using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;
using FIBER.Source.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIBER.Source.Services;

/// <summary>
/// Сервис медиа: дедупликация по ContentHash, хранение BLOB,
/// извлечение во временные файлы %Temp%\FIBER\ для видео/аудио.
/// </summary>
public sealed class MediaService : IMediaService
{
    /// <summary>Допустимые расширения для загрузки.</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
        ".mp4", ".avi", ".wmv",
        ".mp3", ".wav", ".wma"
    };

    /// <summary>Максимальный размер одного файла (500 МБ).</summary>
    private const long MaxFileSizeBytes = 500L * 1024 * 1024;

    /// <summary>Фабрика контекста БД.</summary>
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;

    /// <summary>Сервис аудита.</summary>
    private readonly IAuditLogService _auditLog;

    /// <summary>Сервис БД для прямого потокового чтения BLOB.</summary>
    private readonly IDatabaseService _databaseService;

    /// <summary>Логгер.</summary>
    private readonly ILogger<MediaService> _logger;

    /// <summary>Временный каталог %Temp%\FIBER\</summary>
    private readonly string _tempDir;

    /// <summary>Кэш метаданных «все медиа» — лёгкие объекты без VM и BLOB.
    /// Загружаются один раз при запуске через <see cref="PreloadAllMediaAsync"/>.</summary>
    private IReadOnlyList<(MediaMeta Meta, MediaOwnerType OwnerType, int OwnerId)>? _allMediaMetaCache;

    /// <summary>Кэш путей к Temp-файлам видео/аудио (mediaId → tempPath).
    /// Заполняется при старте и лениво — при первом обращении к медиа.</summary>
    private Dictionary<int, string>? _tempFileCache;

    /// <summary>Кэш VM медиа-элементов, чтобы не пересоздавать при повторном открытии диалога выбора медиа.</summary>
    private readonly Dictionary<int, MediaItemViewModel> _mediaItemCache = new();

    /// <summary>Защита кэшей от параллельного многократного заполнения.</summary>
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>Ограничение параллельного чтения BLOB из SQLCipher (одно соединение к зашифрованной БД).</summary>
    private readonly SemaphoreSlim _blobLoadSemaphore = new(2, 2);

    /// <summary>Инициализирует зависимости и создаёт временный каталог.</summary>
    public MediaService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IAuditLogService auditLog,
        IDatabaseService databaseService,
        ILogger<MediaService> logger)
    {
        _contextFactory = contextFactory;
        _auditLog = auditLog;
        _databaseService = databaseService;
        _logger = logger;
        _tempDir = Path.Combine(Path.GetTempPath(), "FIBER");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>Проверяет, относится ли расширение к изображениям.</summary>
    private static bool IsImageExtension(string extension) =>
        extension.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";

    /// <summary>Копирует выбранные файлы во временный каталог %Temp%\FIBER\ для предпросмотра.</summary>
    public async Task<List<MediaItemViewModel>> SaveToTempAsync(string[] filePaths)
    {
        var result = new List<MediaItemViewModel>();
        if (filePaths is null) return result;

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

                result.Add(new MediaItemViewModel(destPath, Path.GetFileName(path)));
            }
            catch (Exception ex)
            {
                TryDeleteFile(destPath);
                _logger.LogWarning(ex, "Не удалось скопировать {Path} в Temp", path);
            }
        }

        return result;
    }

    /// <summary>Удаляет временные файлы из Temp.</summary>
    public void DeleteTempFiles(IEnumerable<MediaItemViewModel> tempFiles)
    {
        if (tempFiles is null) return;
        foreach (var vm in tempFiles)
        {
            if (vm is null) continue;
            TryDeleteFile(vm.FilePath);
        }
    }

    /// <summary>Фиксирует временные файлы в БД с дедупликацией по SHA-256.</summary>
    public async Task CommitTempFilesAsync(
        List<MediaItemViewModel> tempFiles, MediaOwnerType ownerType, int ownerId)
    {
        if (tempFiles is null || tempFiles.Count == 0) return;

        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var tx = await context.Database.BeginTransactionAsync();

        var deletedTempFiles = new List<string>();
        int addedLinks = 0;

        try
        {
            foreach (var vm in tempFiles)
            {
                if (vm is null || !File.Exists(vm.FilePath)) continue;

                var hash = await ComputeSha256HexAsync(vm.FilePath);

                var existing = await context.MediaAttachments
                    .FirstOrDefaultAsync(m => m.ContentHash == hash);

                MediaAttachment media;
                if (existing is not null)
                {
                    media = existing;
                }
                else
                {
                    var ext = Path.GetExtension(vm.FileName);
                    if (string.IsNullOrEmpty(ext)) ext = Path.GetExtension(vm.FilePath);

                    var data = await File.ReadAllBytesAsync(vm.FilePath);

                    media = new MediaAttachment
                    {
                        OriginalFileName = vm.FileName,
                        MimeType = GetMimeType(ext),
                        SizeBytes = data.LongLength,
                        ContentHash = hash,
                        Data = data,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.MediaAttachments.Add(media);
                    await context.SaveChangesAsync();
                }

                var linkExists = await context.MediaAttachmentLinks
                    .AnyAsync(l => l.MediaAttachmentId == media.Id
                                && l.OwnerType == ownerType
                                && l.OwnerId == ownerId);

                if (!linkExists)
                {
                    context.MediaAttachmentLinks.Add(new MediaAttachmentLink
                    {
                        MediaAttachmentId = media.Id,
                        OwnerType = ownerType,
                        OwnerId = ownerId
                    });
                    addedLinks++;
                }

                deletedTempFiles.Add(vm.FilePath);
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();

            foreach (var path in deletedTempFiles) TryDeleteFile(path);

            await _auditLog.LogAsync("Media.Commit",
                $"Owner={ownerType}:{ownerId}, Files={tempFiles.Count}, Links={addedLinks}");
            InvalidateAllMediaCache();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Ошибка коммита медиа для {Owner}:{OwnerId}", ownerType, ownerId);
            throw;
        }
    }

    /// <summary>Добавляет существующий медиафайл к владельцу; при существующей связи ничего не делает.</summary>
    public async Task AddLinkAsync(int mediaId, MediaOwnerType ownerType, int ownerId)
    {
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

    /// <summary>Возвращает медиа владельца. VM берутся из общего кэша — не создаются заново.</summary>
    public async Task<List<MediaItemViewModel>> GetMediaForOwnerAsync(MediaOwnerType ownerType, int ownerId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var metaList = await context.MediaAttachmentLinks
            .Where(l => l.OwnerType == ownerType && l.OwnerId == ownerId)
            .Select(l => new MediaMeta(
                l.MediaAttachment.Id,
                l.MediaAttachment.OriginalFileName,
                l.MediaAttachment.MimeType,
                l.MediaAttachment.SizeBytes))
            .ToListAsync();

        var result = new List<MediaItemViewModel>(metaList.Count);
        foreach (var meta in metaList)
        {
            var vm = await GetOrCreateVmFromMetaAsync(meta);
            if (vm is not null) result.Add(vm);
        }
        return result;
    }

    /// <summary>Возвращает все медиа со всех владельцев.
    /// VM берутся из кэша, если уже были созданы ранее — не пересоздаются при повторном открытии диалога.</summary>
    public async Task<IReadOnlyList<MediaForOwner>> GetAllMediaAsync()
    {
        var metaList = await GetAllMediaMetaAsync();

        var result = new List<MediaForOwner>(metaList.Count);
        foreach (var (meta, ownerType, ownerId) in metaList)
        {
            var vm = await GetOrCreateVmFromMetaAsync(meta);
            if (vm is not null)
                result.Add(new MediaForOwner(vm, ownerType, ownerId));
        }
        return result;
    }

    /// <summary>Возвращает только изображения со всех владельцев — видео/аудио не извлекаются в Temp.
    /// VM берутся из кэша, если уже были созданы ранее.</summary>
    public async Task<IReadOnlyList<MediaForOwner>> GetAllImagesAsync()
    {
        var metaList = await GetAllMediaMetaAsync();

        var result = new List<MediaForOwner>();
        foreach (var (meta, ownerType, ownerId) in metaList)
        {
            if (!IsImageExtension(Path.GetExtension(meta.OriginalFileName)))
                continue;

            var vm = await GetOrCreateVmFromMetaAsync(meta);
            if (vm is not null)
                result.Add(new MediaForOwner(vm, ownerType, ownerId));
        }
        return result;
    }

    /// <summary>Возвращает VM из кэша или создаёт новый и кэширует его.</summary>
    private async Task<MediaItemViewModel?> GetOrCreateVmFromMetaAsync(MediaMeta meta)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_mediaItemCache.TryGetValue(meta.Id, out var cachedVm))
                return cachedVm;
        }
        finally
        {
            _cacheLock.Release();
        }

        var vm = await CreateVmFromMediaMetaAsync(meta);
        if (vm is not null)
        {
            await _cacheLock.WaitAsync();
            try
            {
                if (!_mediaItemCache.ContainsKey(meta.Id))
                    _mediaItemCache[meta.Id] = vm;
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        return vm;
    }

    /// <summary>Возвращает кэшированные метаданные «все медиа» (без VM, без BLOB).</summary>
    private async Task<IReadOnlyList<(MediaMeta Meta, MediaOwnerType OwnerType, int OwnerId)>> GetAllMediaMetaAsync()
    {
        if (_allMediaMetaCache is not null)
            return _allMediaMetaCache;

        await _cacheLock.WaitAsync();
        try
        {
            if (_allMediaMetaCache is not null)
                return _allMediaMetaCache;

            _allMediaMetaCache = await LoadAllMediaMetaFromDbAsync();
            return _allMediaMetaCache;
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
            .Select(l => new
            {
                Meta = new MediaMeta(
                    l.MediaAttachment.Id,
                    l.MediaAttachment.OriginalFileName,
                    l.MediaAttachment.MimeType,
                    l.MediaAttachment.SizeBytes),
                l.OwnerType,
                l.OwnerId
            })
            .ToListAsync();

        var result = new List<(MediaMeta, MediaOwnerType, int)>(metaList.Count);
        foreach (var item in metaList)
        {
            result.Add((item.Meta, item.OwnerType, item.OwnerId));
        }
        return result;
    }

    /// <summary>Инвалидирует кэши списка «все медиа». Вызывается после любого изменения ссылок.
    /// Сброс ссылок без блокировки — UI-поток не ждёт, если лок удерживается прелоадом.</summary>
    public void InvalidateAllMediaCache()
    {
        _allMediaMetaCache = null;
        _tempFileCache = null;
        lock (_mediaItemCache)
        {
            _mediaItemCache.Clear();
        }
    }

    /// <summary>Предзагружает все медиа в кэш при запуске приложения (один раз).
    /// Создаёт и кэширует MediaItemViewModel для всех медиа, чтобы все экраны
    /// (Search, Update, Settings, Create) использовали одни и те же объекты.
    /// Метаданные — под локом; извлечение видео/аудио — вне лока и на пуле потоков.</summary>
    public async Task PreloadAllMediaAsync()
    {
        IReadOnlyList<(MediaMeta Meta, MediaOwnerType OwnerType, int OwnerId)> metaList;

        await _cacheLock.WaitAsync();
        try
        {
            if (_tempFileCache is not null) return; // уже прелоадили
            if (_allMediaMetaCache is null)
                _allMediaMetaCache = await LoadAllMediaMetaFromDbAsync();
            _tempFileCache = new Dictionary<int, string>(_allMediaMetaCache.Count);
            metaList = _allMediaMetaCache;
        }
        finally
        {
            _cacheLock.Release();
        }

        // Извлекаем видео/аудио во Temp-файлы (вне лока, на пуле потоков)
        foreach (var (meta, _, _) in metaList)
        {
            var ext = Path.GetExtension(meta.OriginalFileName).ToLowerInvariant();
            if (IsImageExtension(ext)) continue;

            await _cacheLock.WaitAsync();
            try
            {
                if (_tempFileCache!.ContainsKey(meta.Id)) continue;
            }
            finally
            {
                _cacheLock.Release();
            }

            try
            {
                var tempPath = await StreamDataToTempAsync(meta.Id, ext);
                if (tempPath is null) continue;

                await _cacheLock.WaitAsync();
                try { _tempFileCache![meta.Id] = tempPath; }
                finally { _cacheLock.Release(); }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось извлечь медиа Id={Id} во Temp при старте", meta.Id);
            }
        }

        // Создаём и кэшируем VM для ВСЕХ медиа — БЕЗ удержания _cacheLock,
        // чтобы избежать deadlock (CreateVmFromMediaMetaAsync сам использует _cacheLock).
        foreach (var (meta, _, _) in metaList)
        {
            await _cacheLock.WaitAsync();
            try
            {
                if (_mediaItemCache.ContainsKey(meta.Id)) continue;
            }
            finally
            {
                _cacheLock.Release();
            }

            var vm = await CreateVmFromMediaMetaAsync(meta);
            if (vm is not null)
            {
                await _cacheLock.WaitAsync();
                try
                {
                    if (!_mediaItemCache.ContainsKey(meta.Id))
                        _mediaItemCache[meta.Id] = vm;
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
        }
    }

    /// <summary>Удаляет связь; если ссылок не осталось, удаляет BLOB и запись.</summary>
    public async Task RemoveLinkAsync(int mediaId, MediaOwnerType ownerType, int ownerId)
    {
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
                var orphan = await context.MediaAttachments
                    .FirstOrDefaultAsync(m => m.Id == mediaId);
                if (orphan is not null)
                {
                    context.MediaAttachments.Remove(orphan);
                    await context.SaveChangesAsync();
                }
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
                var orphans = await context.MediaAttachments
                    .Where(m => orphanIds.Contains(m.Id))
                    .ToListAsync();

                context.MediaAttachments.RemoveRange(orphans);
                await context.SaveChangesAsync();
                removedOrphans = orphans.Count;
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

    /// <summary>Создаёт VM медиа из метаданных: изображения в память, видео/аудио из кэшированного Temp-файла.</summary>
    private async Task<MediaItemViewModel?> CreateVmFromMediaMetaAsync(MediaMeta meta)
    {
        var ext = Path.GetExtension(meta.OriginalFileName).ToLowerInvariant();

        if (IsImageExtension(ext))
        {
            // Ленивая загрузка: BLOB не читаем из базы здесь — сделаем это в LoadImageAsync по требованию
            return new MediaItemViewModel(this, meta.Id, meta.OriginalFileName) { MediaId = meta.Id };
        }

        // Видео/аудио — ищем в кэше Temp-файлов
        string? tempPath = null;
        await _cacheLock.WaitAsync();
        try
        {
            if (_tempFileCache is not null && _tempFileCache.TryGetValue(meta.Id, out var cachedPath))
            {
                if (File.Exists(cachedPath))
                    tempPath = cachedPath;
                else
                    _tempFileCache.Remove(meta.Id);
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        if (tempPath is null)
        {
            try
            {
                tempPath = await StreamDataToTempAsync(meta.Id, ext);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось извлечь медиа Id={Id} во временный файл", meta.Id);
                await _auditLog.LogAsync("Media.ExtractFailed", $"MediaId={meta.Id}");
                return null;
            }

            if (tempPath is null) return null;

            // Только что извлечённый файл кладём в кэш, чтобы не извлекать повторно.
            await _cacheLock.WaitAsync();
            try
            {
                _tempFileCache ??= new Dictionary<int, string>();
                _tempFileCache[meta.Id] = tempPath;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        return new MediaItemViewModel(tempPath, meta.OriginalFileName)
        {
            MediaId = meta.Id,
            DeleteOnDispose = false // файл кэшированный — не удаляем при закрытии VM
        };
    }

    /// <summary>
    /// Потоково копирует BLOB в Temp-файл без загрузки всего массива в память.
    /// Вся работа с SqliteConnection выполняется на пуле потоков: <c>SqliteBlobStream</c>
    /// не поддерживает асинхронное чтение, поэтому <c>CopyToAsync</c> внутри всё равно
    /// синхронно читает BLOB. Обёртка в <see cref="Task.Run(Action)"/> не даёт UI-потоку
    /// зависнуть на всё время извлечения.
    /// </summary>
    private async Task<string?> StreamDataToTempAsync(int mediaId, string extension)
    {
        var tempPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + extension);

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = _databaseService.DatabasePath,
            Password = _databaseService.CurrentPassword,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };
        var connectionString = csb.ToString();

        try
        {
            await Task.Run(() =>
            {
                using var conn = new SqliteConnection(connectionString);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Data FROM MediaAttachments WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", mediaId);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException($"MediaAttachment Id={mediaId} не найден.");

                using var stream = reader.GetStream(0);
                using var fileStream = File.Create(tempPath);
                stream.CopyTo(fileStream);
            });
            return tempPath;
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    /// <summary>Читает BLOB-данные вложения по идентификатору (для ленивой загрузки).
    /// SQLite-провайдер читает BLOB синхронно внутри, поэтому вся операция выносится на пул потоков.
    /// Чтение защищено семафором — к SQLCipher можно обращаться не более 2 параллельных потоков,
    /// иначе возникают конфликты при работе с зашифрованным файлом БД.</summary>
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

    /// <summary>Вычисляет SHA-256 hex содержимого файла.</summary>
    private static async Task<string> ComputeSha256HexAsync(string path)
    {
        await using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs);
        return Convert.ToHexString(hash);
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
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".wma" => "audio/x-ms-wma",
        _ => "application/octet-stream"
    };

    /// <summary>Пытается удалить файл, игнорирует ошибки.</summary>
    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    /// <summary>Метаданные медиа без BLOB-поля.</summary>
    private sealed record MediaMeta(int Id, string OriginalFileName, string MimeType, long SizeBytes);
}