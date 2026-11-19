using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FIBER.Source.Contracts;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIBER.Source.Services;

/// <summary>
/// Сервис медиа: дедупликация по ContentHash, хранение BLOB,
/// извлечение во временные файлы %Temp%\FIBER\ для видео/аудио.
/// Сервис не зависит от слоя представления — возвращает чистые DTO (MediaItemDto).
/// </summary>
public sealed class MediaService : IMediaService
{
    /// <summary>Допустимые расширения для загрузки.</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
        ".mp4", ".avi", ".wmv", ".mkv", ".mov", ".flv", ".webm", ".m4v",
        ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aiff", ".aif"
    };

    /// <summary>Максимальный размер одного файла (500 МБ).</summary>
    private const long MaxFileSizeBytes = 500L * 1024 * 1024;

    /// <summary>Фабрика контекста БД.</summary>
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;

    /// <summary>Сервис аудита.</summary>
    private readonly IAuditLogService _auditLog;

    /// <summary>Логгер.</summary>
    private readonly ILogger<MediaService> _logger;

    /// <summary>Временный каталог %Temp%\FIBER\</summary>
    private readonly string _tempDir;

    /// <summary>Кэш метаданных «все медиа» — лёгкие объекты без VM и BLOB.
    /// Загружаются один раз при запуске через <see cref="PreloadAllMediaAsync"/>.</summary>
    private IReadOnlyList<(MediaMeta Meta, MediaOwnerType OwnerType, int OwnerId)>? _allMediaMetaCache;

    private readonly object _metadataSync = new();
    private long _cacheVersion;

    /// <summary>Защита кэшей от параллельного многократного заполнения.</summary>
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>Ограничение параллельного чтения BLOB для контроля нагрузки и памяти.</summary>
    private readonly SemaphoreSlim _blobLoadSemaphore = new(2, 2);

    /// <summary>Инициализирует зависимости; каталог создаётся при первой записи.</summary>
    public MediaService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IAuditLogService auditLog,
        ILogger<MediaService> logger)
    {
        _contextFactory = contextFactory;
        _auditLog = auditLog;
        _logger = logger;
        _tempDir = TemporaryFiles.SessionDirectory;
    }

    /// <summary>Проверяет, относится ли расширение к изображениям.</summary>
    private static bool IsImageExtension(string extension) =>
        extension.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";

    private static bool IsVideoExtension(string extension) =>
        extension.ToLowerInvariant() is ".mp4" or ".avi" or ".wmv" or ".mkv" or ".mov" or ".flv" or ".webm" or ".m4v";

    private static bool IsAudioExtension(string extension) =>
        extension.ToLowerInvariant() is ".mp3" or ".wav" or ".wma" or ".m4a" or ".aac" or ".flac" or ".ogg" or ".aiff" or ".aif";

    /// <summary>Копирует выбранные файлы во временный каталог %Temp%\FIBER\ для предпросмотра.</summary>
    public async Task<List<MediaItemDto>> SaveToTempAsync(string[] filePaths)
    {
        await BackgroundWork.SwitchToThreadPool();
        var result = new List<MediaItemDto>();
        if (filePaths is null) return result;
        Directory.CreateDirectory(_tempDir);

        // Используем один контекст БД на все файлы для проверки существующих медиа по ContentHash
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Загружаем существующие наименования из БД, чтобы генерировать уникальные имена
        // уже при добавлении файла (Drag&Drop), а не только при коммите.
        var existingNames = await context.MediaAttachments
            .Select(m => m.OriginalFileName)
            .ToListAsync();
        var nameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;

            var ext = Path.GetExtension(path);
            if (!AllowedExtensions.Contains(ext))
            {
                _logger.LogInformation("Пропущен {Path}: расширение {Ext} не поддерживается", path, ext);
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length > MaxFileSizeBytes)
            {
                _logger.LogInformation("Пропущен {Path}: размер превышает лимит", path);
                continue;
            }

            var tempFileName = Guid.NewGuid().ToString("N") + ext;
            var destPath = Path.Combine(_tempDir, tempFileName);

            try
            {
                await using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
                await using (var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await src.CopyToAsync(dst);
                }

                var hash = await ComputeFileHashAsync(destPath);

                // Генерируем уникальное отображаемое имя уже при добавлении файла,
                // чтобы не было дублей наименований в списке временных файлов.
                var uniqueFileName = GenerateUniqueFileName(Path.GetFileName(path), ext, nameSet);

                // Проверяем, существует ли такой файл уже в БД (по ContentHash).
                var existingMediaId = await context.MediaAttachments
                    .Where(m => m.ContentHash == hash)
                    .Select(m => (int?)m.Id)
                    .FirstOrDefaultAsync();

                result.Add(new MediaItemDto(
                    MediaId: existingMediaId,
                    FilePath: destPath,
                    OriginalFileName: uniqueFileName,
                    MimeType: GetMimeType(ext),
                    SizeBytes: info.Length,
                    ContentHash: hash,
                    IsImage: IsImageExtension(ext),
                    IsVideo: IsVideoExtension(ext),
                    IsAudio: IsAudioExtension(ext)));
            }
            catch (Exception ex)
            {
                await TemporaryFiles.DeleteAsync(destPath);
                _logger.LogWarning(ex, "Не удалось скопировать {Path} в Temp", path);
            }
        }

        return result;
    }

    /// <summary>Удаляет временные файлы из Temp.</summary>
    public async Task DeleteTempFilesAsync(IEnumerable<MediaItemDto> tempFiles)
    {
        if (tempFiles is null) return;
        foreach (var dto in tempFiles)
        {
            if (!string.IsNullOrWhiteSpace(dto.FilePath))
                await TemporaryFiles.DeleteAsync(dto.FilePath);
        }
    }

    /// <summary>Фиксирует временные файлы в БД с дедупликацией по SHA-256.</summary>
    public async Task CommitTempFilesAsync(
        List<MediaItemDto> tempFiles, MediaOwnerType ownerType, int ownerId)
    {
        await BackgroundWork.SwitchToThreadPool();
        if (tempFiles is null || tempFiles.Count == 0) return;

        await using var context = await _contextFactory.CreateDbContextAsync();

        var deletedTempFiles = new List<string>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var processedHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dto in tempFiles)
        {
            if (dto is null) continue;
            if (!File.Exists(dto.FilePath))
                throw new FileNotFoundException("Временное вложение отсутствует. Добавьте файл повторно.", dto.FilePath);

            // Проверяем дедупликацию по ContentHash
            var hash = await ComputeSha256HexAsync(dto.FilePath);
            if (!processedHashes.Add(hash))
            {
                deletedTempFiles.Add(dto.FilePath);
                continue;
            }
            var existing = await context.MediaAttachments
                .Select(m => new { m.Id, m.ContentHash })
                .FirstOrDefaultAsync(m => m.ContentHash == hash);

            if (existing is not null)
            {
                // Файл уже есть в БД — создаём только ссылку
                var linkExists = await context.MediaAttachmentLinks
                    .AnyAsync(l => l.MediaAttachmentId == existing.Id
                                    && l.OwnerType == ownerType
                                    && l.OwnerId == ownerId);

                if (!linkExists)
                {
                    context.MediaAttachmentLinks.Add(new MediaAttachmentLink
                    {
                        MediaAttachmentId = existing.Id,
                        OwnerType = ownerType,
                        OwnerId = ownerId
                    });
                }

                deletedTempFiles.Add(dto.FilePath);
                continue;
            }

            // Создаём новую запись медиа
            var ext = Path.GetExtension(dto.OriginalFileName);
            if (string.IsNullOrEmpty(ext)) ext = Path.GetExtension(dto.FilePath);

            var data = await File.ReadAllBytesAsync(dto.FilePath);

            var media = new MediaAttachment
            {
                OriginalFileName = dto.OriginalFileName,
                MimeType = GetMimeType(ext),
                SizeBytes = data.LongLength,
                ContentHash = hash,
                Data = data,
                CreatedAt = DateTime.UtcNow
            };
            context.MediaAttachments.Add(media);
            // Сохраняем медиа, чтобы получить реальный ID перед созданием ссылки
            await context.SaveChangesAsync();
            context.MediaAttachmentLinks.Add(new MediaAttachmentLink
            {
                MediaAttachmentId = media.Id,
                OwnerType = ownerType,
                OwnerId = ownerId
            });

            deletedTempFiles.Add(dto.FilePath);
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        foreach (var path in deletedTempFiles) await TemporaryFiles.DeleteAsync(path);

        await _auditLog.LogAsync("Media.Commit",
            $"Owner={ownerType}:{ownerId}, Files={tempFiles.Count}");
        InvalidateAllMediaCache();
    }

    /// <summary>Добавляет существующий медиафайл к владельцу; при существующей связи ничего не делает.</summary>
    public async Task AddLinkAsync(int mediaId, MediaOwnerType ownerType, int ownerId)
    {
        await BackgroundWork.SwitchToThreadPool();
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var tx = await context.Database.BeginTransactionAsync();

        try
        {
            var linkExists = await context.MediaAttachmentLinks
                .AnyAsync(l => l.MediaAttachmentId == mediaId
                                && l.OwnerType == ownerType
                                && l.OwnerId == ownerId);

            if (!linkExists)
            {
                context.MediaAttachmentLinks.Add(new MediaAttachmentLink
                {
                    MediaAttachmentId = mediaId,
                    OwnerType = ownerType,
                    OwnerId = ownerId
                });
                await context.SaveChangesAsync();
            }

            await tx.CommitAsync();
            await _auditLog.LogAsync("Media.AddLink", $"MediaId={mediaId}, Owner={ownerType}:{ownerId}");
            InvalidateAllMediaCache();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Ошибка добавления связи {MediaId}", mediaId);
            throw;
        }
    }

    /// <summary>Проверяет, привязан ли медиафайл уже к данному владельцу.</summary>
    public async Task<bool> IsMediaLinkedToOwnerAsync(int mediaId, MediaOwnerType ownerType, int ownerId)
    {
        await BackgroundWork.SwitchToThreadPool();
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MediaAttachmentLinks
            .AnyAsync(l => l.MediaAttachmentId == mediaId
                            && l.OwnerType == ownerType
                            && l.OwnerId == ownerId);
    }

    /// <summary>Возвращает медиа владельца.</summary>
    public async Task<List<MediaItemDto>> GetMediaForOwnerAsync(MediaOwnerType ownerType, int ownerId)
    {
        await BackgroundWork.SwitchToThreadPool();
        await using var context = await _contextFactory.CreateDbContextAsync();

        var metas = await context.MediaAttachmentLinks
            .Where(l => l.OwnerType == ownerType && l.OwnerId == ownerId)
            .Select(l => new
            {
                l.MediaAttachment.Id,
                l.MediaAttachment.OriginalFileName,
                l.MediaAttachment.MimeType,
                l.MediaAttachment.SizeBytes,
                l.MediaAttachment.ContentHash
            })
            .ToListAsync();

        var result = new List<MediaItemDto>(metas.Count);
        foreach (var m in metas)
        {
            var ext = Path.GetExtension(m.OriginalFileName).ToLowerInvariant();

            result.Add(new MediaItemDto(
                MediaId: m.Id,
                FilePath: string.Empty,
                OriginalFileName: m.OriginalFileName,
                MimeType: m.MimeType,
                SizeBytes: m.SizeBytes,
                ContentHash: m.ContentHash,
                IsImage: IsImageExtension(ext),
                IsVideo: IsVideoExtension(ext),
                IsAudio: IsAudioExtension(ext)));
        }

        return result;
    }

    /// <summary>Возвращает все медиа со всех владельцев.</summary>
    public async Task<IReadOnlyList<MediaForOwner>> GetAllMediaAsync()
    {
        await BackgroundWork.SwitchToThreadPool();
        var metaList = await GetAllMediaMetaAsync();

        var result = new List<MediaForOwner>(metaList.Count);
        foreach (var (meta, ownerType, ownerId) in metaList)
        {
            var ext = Path.GetExtension(meta.OriginalFileName).ToLowerInvariant();

            result.Add(new MediaForOwner(
                new MediaItemDto(
                    MediaId: meta.Id,
                    FilePath: string.Empty,
                    OriginalFileName: meta.OriginalFileName,
                    MimeType: meta.MimeType,
                    SizeBytes: meta.SizeBytes,
                    ContentHash: string.Empty,
                    IsImage: IsImageExtension(ext),
                    IsVideo: IsVideoExtension(ext),
                    IsAudio: IsAudioExtension(ext)),
                ownerType,
                ownerId));
        }

        return result;
    }

    /// <summary>Возвращает только изображения со всех владельцев — видео/аудио не извлекаются в Temp.</summary>
    public async Task<IReadOnlyList<MediaForOwner>> GetAllImagesAsync()
    {
        await BackgroundWork.SwitchToThreadPool();
        var metaList = await GetAllMediaMetaAsync();

        var result = new List<MediaForOwner>();
        foreach (var (meta, ownerType, ownerId) in metaList)
        {
            if (!IsImageExtension(Path.GetExtension(meta.OriginalFileName)))
                continue;

            result.Add(new MediaForOwner(
                new MediaItemDto(
                    MediaId: meta.Id,
                    FilePath: string.Empty,
                    OriginalFileName: meta.OriginalFileName,
                    MimeType: meta.MimeType,
                    SizeBytes: meta.SizeBytes,
                    ContentHash: string.Empty,
                    IsImage: true,
                    IsVideo: false,
                    IsAudio: false),
                ownerType,
                ownerId));
        }

        return result;
    }

    /// <summary>Инвалидирует кэши списка «все медиа». Вызывается после любого изменения ссылок.</summary>
    public void InvalidateAllMediaCache()
    {
        lock (_metadataSync)
        {
            _cacheVersion++;
            _allMediaMetaCache = null;
        }
    }

    /// <summary>Предзагружает метаданные без извлечения аудио/видео.</summary>
    public async Task PreloadAllMediaAsync()
    {
        await BackgroundWork.SwitchToThreadPool();
        try
        {
            await GetAllMediaMetaAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось предзагрузить метаданные медиа");
        }
    }

    /// <summary>Удаляет связь; если ссылок не осталось, удаляет BLOB и запись.</summary>
    public async Task RemoveLinkAsync(int mediaId, MediaOwnerType ownerType, int ownerId)
    {
        await BackgroundWork.SwitchToThreadPool();
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var tx = await context.Database.BeginTransactionAsync();

        try
        {
            var link = await context.MediaAttachmentLinks
                .FirstOrDefaultAsync(l => l.MediaAttachmentId == mediaId
                                           && l.OwnerType == ownerType
                                           && l.OwnerId == ownerId);

            if (link is null)
            {
                await tx.CommitAsync();
                return;
            }

            context.MediaAttachmentLinks.Remove(link);
            await context.SaveChangesAsync();

            var hasOtherLinks = await context.MediaAttachmentLinks
                .AnyAsync(l => l.MediaAttachmentId == mediaId);

            if (!hasOtherLinks)
            {
                await context.MediaAttachments.Where(m => m.Id == mediaId).ExecuteDeleteAsync();
            }

            await tx.CommitAsync();
            await _auditLog.LogAsync("Media.RemoveLink",
                $"MediaId={mediaId}, Owner={ownerType}:{ownerId}, Orphaned={!hasOtherLinks}");
            InvalidateAllMediaCache();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Ошибка удаления связи {MediaId}", mediaId);
            throw;
        }
    }

    /// <summary>Удаляет все связи владельца и осиротевшие MediaAttachment.</summary>
    public async Task RemoveAllLinksForOwnerAsync(MediaOwnerType ownerType, int ownerId)
    {
        await BackgroundWork.SwitchToThreadPool();
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var tx = await context.Database.BeginTransactionAsync();

        int removedLinks = 0;
        int removedOrphans = 0;

        try
        {
            var links = await context.MediaAttachmentLinks
                .Where(l => l.OwnerType == ownerType && l.OwnerId == ownerId)
                .ToListAsync();

            if (links.Count == 0)
            {
                await tx.CommitAsync();
                return;
            }

            removedLinks = links.Count;
            var mediaIds = links.Select(l => l.MediaAttachmentId).Distinct().ToList();

            context.MediaAttachmentLinks.RemoveRange(links);
            await context.SaveChangesAsync();

            var orphanIds = await context.MediaAttachments
                .Where(m => mediaIds.Contains(m.Id))
                .Where(m => !m.Links.Any())
                .Select(m => m.Id)
                .ToListAsync();

            if (orphanIds.Count > 0)
            {
                removedOrphans = await context.MediaAttachments
                    .Where(m => orphanIds.Contains(m.Id)).ExecuteDeleteAsync();
            }

            await tx.CommitAsync();
            await _auditLog.LogAsync("Media.RemoveAllForOwner",
                $"Owner={ownerType}:{ownerId}, Links={removedLinks}, Orphans={removedOrphans}");
            InvalidateAllMediaCache();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Ошибка удаления всех медиа для {Owner}:{OwnerId}", ownerType, ownerId);
            throw;
        }
    }

    /// <summary>Читает BLOB-данные вложения по идентификатору (для ленивой загрузки).
    /// SQLite-провайдер читает BLOB синхронно внутри, поэтому вся операция выносится на пул потоков.
    /// Семафор ограничивает количество одновременно выделяемых больших массивов.</summary>
    public async Task<byte[]?> GetMediaDataAsync(int mediaId)
    {
        await _blobLoadSemaphore.WaitAsync();
        try
        {
            return await Task.Run(async () =>
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                return await context.MediaAttachments
                    .Where(m => m.Id == mediaId)
                    .Select(m => m.Data)
                    .FirstOrDefaultAsync();
            });
        }
        finally
        {
            _blobLoadSemaphore.Release();
        }
    }

    /// <summary>Вычисляет SHA-256 hex содержимого файла (для защиты от дублей).</summary>
    public async Task<string> ComputeFileHashAsync(string filePath)
    {
        await BackgroundWork.SwitchToThreadPool();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return string.Empty;
        return await ComputeSha256HexAsync(filePath);
    }

    /// <summary>Ищет MediaId по ContentHash в БД. Возвращает null, если файл не найден.</summary>
    public async Task<int?> GetMediaIdByContentHashAsync(string contentHash)
    {
        await BackgroundWork.SwitchToThreadPool();
        if (string.IsNullOrWhiteSpace(contentHash)) return null;

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MediaAttachments
            .Where(m => m.ContentHash == contentHash)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>Вычисляет SHA-256 hex содержимого файла.</summary>
    private static async Task<string> ComputeSha256HexAsync(string path)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs);
        return Convert.ToHexString(hash);
    }

    public async Task<string> GetMediaFilePathAsync(int mediaId)
    {
        await BackgroundWork.SwitchToThreadPool();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var name = await context.MediaAttachments.Where(m => m.Id == mediaId)
            .Select(m => m.OriginalFileName).SingleAsync();
        var extension = Path.GetExtension(name);
        if (!IsVideoExtension(extension) && !IsAudioExtension(extension))
            throw new InvalidOperationException("Файл не является видео или аудио.");
        await _blobLoadSemaphore.WaitAsync();
        try { return await StreamDataToTempAsync(mediaId, extension) ?? string.Empty; }
        finally { _blobLoadSemaphore.Release(); }
    }

    /// <summary>Возвращает MIME-тип по расширению.</summary>
    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".avi" => "video/x-msvideo",
        ".wmv" => "video/x-ms-wmv",
        ".mkv" => "video/x-matroska",
        ".mov" => "video/quicktime",
        ".flv" => "video/x-flv",
        ".webm" => "video/webm",
        ".m4v" => "video/x-m4v",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".wma" => "audio/x-ms-wma",
        ".m4a" => "audio/mp4",
        ".aac" => "audio/aac",
        ".flac" => "audio/flac",
        ".ogg" => "audio/ogg",
        ".aiff" => "audio/aiff",
        ".aif" => "audio/aiff",
        _ => "application/octet-stream"
    };

    /// <summary>Генерирует уникальное отображаемое имя медиафайла, не совпадающее ни с одним существующим в БД.
    /// Имя генерируется полностью со случайным суффиксом, без использования исходного имени файла.</summary>
    private static string GenerateUniqueFileName(
        string proposedFileName,
        string extension,
        HashSet<string> nameSet)
    {
        string candidate;
        do
        {
            var suffix = GenerateRandomSuffix(12);
            candidate = $"{suffix}{extension}";
        }
        while (nameSet.Contains(candidate));

        nameSet.Add(candidate);
        return candidate;
    }

    /// <summary>Генерирует случайную строку заданной длины из цифр и латиницы (a-z, A-Z, 0-9).</summary>
    private static string GenerateRandomSuffix(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
        return sb.ToString();
    }

    /// <summary>Потоково копирует BLOB в Temp-файл без загрузки всего массива в память.</summary>
    private async Task<string?> StreamDataToTempAsync(int mediaId, string extension)
    {
        var tempPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + extension);

        try
        {
            Directory.CreateDirectory(_tempDir);
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Database.OpenConnectionAsync();
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            // SqliteBlob использует incremental blob I/O: SELECT Data не материализует весь файл.
            await using var stream = new SqliteBlob(connection, "MediaAttachments", "Data", mediaId, readOnly: true);
            await using var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await stream.CopyToAsync(fileStream);
            return tempPath;
        }
        catch
        {
            await TemporaryFiles.DeleteAsync(tempPath);
            throw;
        }
    }

    /// <summary>Возвращает кэшированные метаданные «все медиа» (без VM, без BLOB).</summary>
    private async Task<IReadOnlyList<(MediaMeta Meta, MediaOwnerType OwnerType, int OwnerId)>> GetAllMediaMetaAsync()
    {
        lock (_metadataSync)
        {
            if (_allMediaMetaCache is not null) return _allMediaMetaCache;
        }

        await _cacheLock.WaitAsync();
        try
        {
            long version;
            lock (_metadataSync)
            {
                if (_allMediaMetaCache is not null) return _allMediaMetaCache;
                version = _cacheVersion;
            }
            var loaded = await LoadAllMediaMetaFromDbAsync();
            lock (_metadataSync)
            {
                if (version == _cacheVersion) _allMediaMetaCache = loaded;
            }
            return loaded;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>Загружает метаданные всех медиа из БД (без кэша, без VM).</summary>
    private async Task<IReadOnlyList<(MediaMeta Meta, MediaOwnerType OwnerType, int OwnerId)>> LoadAllMediaMetaFromDbAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var metaList = await context.MediaAttachmentLinks
            .GroupBy(l => l.MediaAttachmentId)
            .Select(g => new
            {
                Meta = new MediaMeta(
                    g.First().MediaAttachment.Id,
                    g.First().MediaAttachment.OriginalFileName,
                    g.First().MediaAttachment.MimeType,
                    g.First().MediaAttachment.SizeBytes),
                OwnerType = g.First().OwnerType,
                OwnerId = g.First().OwnerId
            })
            .ToListAsync();

        var result = new List<(MediaMeta, MediaOwnerType, int)>(metaList.Count);
        foreach (var item in metaList)
        {
            result.Add((item.Meta, item.OwnerType, item.OwnerId));
        }
        return result;
    }

    /// <summary>Метаданные медиа без BLOB-поля.</summary>
    private sealed record MediaMeta(int Id, string OriginalFileName, string MimeType, long SizeBytes);
}
