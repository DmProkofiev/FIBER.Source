using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
                await using (var src = File.OpenRead(path))
                await using (var dst = File.Create(destPath))
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

            // Временные файлы больше не нужны — данные в БД
            foreach (var path in deletedTempFiles) TryDeleteFile(path);

            await _auditLog.LogAsync("Media.Commit",
                $"Owner={ownerType}:{ownerId}, Files={tempFiles.Count}, Links={addedLinks}");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Ошибка коммита медиа для {Owner}:{OwnerId}", ownerType, ownerId);
            throw;
        }
    }

    /// <summary>Возвращает медиа владельца.</summary>
    public async Task<List<MediaItemViewModel>> GetMediaForOwnerAsync(MediaOwnerType ownerType, int ownerId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Загружаем только метаданные, чтобы не тянуть BLOB в память для видео/аудио
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
            var vm = await CreateVmFromMediaMetaAsync(context, meta);
            if (vm is not null) result.Add(vm);
        }
        return result;
    }

    /// <summary>Возвращает все медиа со всех владельцев.</summary>
    public async Task<IReadOnlyList<MediaForOwner>> GetAllMediaAsync()
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

        var result = new List<MediaForOwner>(metaList.Count);
        foreach (var item in metaList)
        {
            var vm = await CreateVmFromMediaMetaAsync(context, item.Meta);
            if (vm is not null)
                result.Add(new MediaForOwner(vm, item.OwnerType, item.OwnerId));
        }
        return result;
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
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Ошибка удаления всех медиа для {Owner}:{OwnerId}", ownerType, ownerId);
            throw;
        }
    }

    /// <summary>Создаёт VM медиа из метаданных: изображения в память, видео/аудио в Temp-файл.</summary>
    private async Task<MediaItemViewModel?> CreateVmFromMediaMetaAsync(
        FiberSourceDbContext context, MediaMeta meta)
    {
        var ext = Path.GetExtension(meta.OriginalFileName).ToLowerInvariant();
        var isImage = ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";

        if (isImage)
        {
            // Изображение — читаем BLOB в память (нужен для BitmapImage)
            var data = await context.MediaAttachments
                .Where(m => m.Id == meta.Id)
                .Select(m => m.Data)
                .FirstOrDefaultAsync();

            if (data is null) return null;
            return new MediaItemViewModel(data, meta.OriginalFileName) { MediaId = meta.Id };
        }

        // Видео/аудио — потоковое извлечение во временный файл
        try
        {
            var tempPath = await StreamDataToTempAsync(meta.Id, ext);
            if (tempPath is null) return null;

            return new MediaItemViewModel(tempPath, meta.OriginalFileName)
            {
                MediaId = meta.Id,
                DeleteOnDispose = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось извлечь медиа Id={Id} во временный файл", meta.Id);
            await _auditLog.LogAsync("Media.ExtractFailed", $"MediaId={meta.Id}");
            return null;
        }
    }

    /// <summary>Потоково копирует BLOB в Temp-файл без загрузки всего массива в память.</summary>
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

        await using var conn = new SqliteConnection(csb.ToString());
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Data FROM MediaAttachments WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", mediaId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            TryDeleteFile(tempPath);
            return null;
        }

        await using var stream = reader.GetStream(0);
        await using var fileStream = File.Create(tempPath);
        await stream.CopyToAsync(fileStream);

        return tempPath;
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