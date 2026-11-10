using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.MediaSelection;
using FIBER.Source.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.Services.MediaManagement;

/// Реализация общих операций с медиа для Update-экранов
public sealed class MediaManagementService : IMediaManagementService
{
    private readonly IMediaService _mediaService;
    private readonly IMediaSelectionDialogService _dialogService;

    public MediaManagementService(IMediaService mediaService, IMediaSelectionDialogService dialogService)
    {
        _mediaService = mediaService;
        _dialogService = dialogService;
    }

    /// Загружает существующие медиа владельца, сгруппированные по типам
    public async Task<OwnerMedia> LoadOwnerMediaAsync(MediaOwnerType ownerType, int ownerId)
    {
        var media = new OwnerMedia();
        var mediaList = await _mediaService.GetMediaForOwnerAsync(ownerType, ownerId);

        foreach (var m in mediaList)
        {
            if (m.IsImage) media.Images.Add(m);
            else if (m.IsVideo) media.Videos.Add(m);
            else if (m.IsAudio) media.Audios.Add(m);

            if (m.IsImage)
                _ = m.LoadImageAsync();
        }

        return media;
    }

    /// Очищает коллекции существующих медиа.
    /// Не утилизирует VM — они belong к общему кэшу MediaService и используются повторно
    public void ClearMedia(OwnerMedia media)
    {
        media.Images.Clear();
        media.Videos.Clear();
        media.Audios.Clear();
    }

    /// <summary>Открывает диалог выбора существующих медиафайлов и возвращает выбранные.
    /// Возвращает выбранные медиа для добавления во TempFiles (ручное добавление),
    /// а не прямую привязку к владельцу в БД.
    /// Пустой список — диалог отменён или ничего не выбрано.
    /// alreadyAddedMediaIds — медиа, уже добавленные в TempFiles (для Create-экранов),
    /// они будут помечены галочкой и исключены из результата выбора.</summary>
    public async Task<IReadOnlyList<MediaItemViewModel>> SelectExistingMediaAsync(IReadOnlyCollection<int>? alreadyAddedMediaIds = null)
    {
        var allMedia = await _mediaService.GetAllMediaAsync();
        var items = allMedia.Where(m => m.Media is not null).Select(m => m.Media!).ToList();

        if (items.Count == 0)
        {
            Notification.Show("Медиа", "В базе нет медиафайлов", FIBER.Source.Helpers.NotificationType.Info);
            return Array.Empty<MediaItemViewModel>();
        }

        var selectedIds = await _dialogService.ShowSelectMediaAsync(items, alreadyAddedMediaIds ?? Array.Empty<int>());
        if (selectedIds.Count == 0) return Array.Empty<MediaItemViewModel>();

        // Возвращаем выбранные медиа-элементы для добавления во TempFiles ViewModel
        return items.Where(m => selectedIds.Contains(m.MediaId ?? 0)).ToList();
    }

    /// <summary>Открывает диалог выбора существующих медиафайлов для владельца.
    /// Файлы, уже принадлежащие владельцу, помечаются галочкой — повторно добавлять их нельзя.</summary>
    public async Task<IReadOnlyList<MediaItemViewModel>> SelectExistingMediaAsync(MediaOwnerType ownerType, int ownerId,
        IReadOnlyCollection<int>? alreadyAddedMediaIds = null)
    {
        var allMedia = await _mediaService.GetAllMediaAsync();
        var items = allMedia.Where(m => m.Media is not null).Select(m => m.Media!).ToList();

        if (items.Count == 0)
        {
            Notification.Show("Медиа", "В базе нет медиафайлов", FIBER.Source.Helpers.NotificationType.Info);
            return Array.Empty<MediaItemViewModel>();
        }

        // Объединяем уже привязанные к владельцу (из БД) с уже добавленными во TempFiles (из Drag&Drop или предыдущего вызова)
        var alreadyAdded = new HashSet<int>();
        var ownerMedia = await _mediaService.GetMediaForOwnerAsync(ownerType, ownerId);
        foreach (var m in ownerMedia)
        {
            if (m.MediaId is int id)
                alreadyAdded.Add(id);
        }
        if (alreadyAddedMediaIds != null)
        {
            foreach (var id in alreadyAddedMediaIds)
                alreadyAdded.Add(id);
        }

        var selectedIds = await _dialogService.ShowSelectMediaAsync(items, alreadyAdded);
        if (selectedIds.Count == 0) return Array.Empty<MediaItemViewModel>();

        return items.Where(m => selectedIds.Contains(m.MediaId ?? 0)).ToList();
    }

    /// <summary>Добавляет медиа во временные файлы с защитой от дублей
    /// (совпадение по MediaId, ContentHash или пути к файлу — один и тот же файл не попадает в список дважды).</summary>
    public Task AddToTempFilesAsync(ObservableCollection<MediaItemViewModel> tempFiles, IEnumerable<MediaItemViewModel> media)
    {
        var list = media?.Where(m => m is not null).ToList() ?? new List<MediaItemViewModel>();
        foreach (var file in list)
        {
            if (tempFiles.Any(t => BelongsToSameMedia(t, file))) continue;
            tempFiles.Add(file);
        }
        return Task.CompletedTask;
    }

    /// <summary>Проверка «один и тот же медиа»: совпадение по идентификатору, контенту или пути к файлу.</summary>
    private static bool BelongsToSameMedia(MediaItemViewModel a, MediaItemViewModel b)
    {
        if (a is null || b is null) return false;

        // По идентификатору вложения в БД
        if (a.MediaId.HasValue && b.MediaId.HasValue && a.MediaId == b.MediaId) return true;

        // По SHA-256 содержимому (для файлов без MediaId, загруженных через Drag&Drop)
        if (!string.IsNullOrEmpty(a.ContentHash) && !string.IsNullOrEmpty(b.ContentHash)
            && a.ContentHash == b.ContentHash) return true;

        // По пути к Temp-файлу (для файлов, скопированных из одного и того же источника)
        if (!string.IsNullOrEmpty(a.FilePath) && !string.IsNullOrEmpty(b.FilePath)
            && string.Equals(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    /// Привязывает существующие медиафайлы (по MediaId) к владельцу в БД
    /// Уже привязанные к владельцу файлы пропускаются (без дублей)
    /// Возвращает число пропущенных (уже привязанных) файлов — для уведомления пользователя
    public async Task<int> LinkExistingMediaAsync(IEnumerable<int> mediaIds, MediaOwnerType ownerType, int ownerId)
    {
        var ids = mediaIds is null ? new List<int>() : mediaIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return 0;

        int skipped = 0;
        foreach (var mediaId in ids)
        {
            if (await _mediaService.IsMediaLinkedToOwnerAsync(mediaId, ownerType, ownerId))
            {
                skipped++;
                continue;
            }
            await _mediaService.AddLinkAsync(mediaId, ownerType, ownerId);
        }
        return skipped;
    }

    /// Удаляет существующий медиафайл у владельца
    public async Task DeleteExistingMediaAsync(MediaOwnerType ownerType, int ownerId, MediaItemViewModel media)
    {
        if (media == null || media.MediaId is null) return;
        if (!FIBER.Source.Helpers.Notification.Confirm("Подтверждение", $"Удалить файл {media.FileName}?")) return;

        try
        {
            await _mediaService.RemoveLinkAsync(media.MediaId.Value, ownerType, ownerId);
            media.Dispose();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка удаления: {ex.Message}", FIBER.Source.Helpers.NotificationType.Error);
        }
    }

    /// Обрабатывает перетаскивание файлов (добавляет во временные файлы).
    /// Защита от дубля: один и тот же файл (по SHA-256 содержимому) не может быть
    /// перетащен во временные файлы более одного раза
    public async Task HandleFilesDropAsync(ObservableCollection<MediaItemViewModel> tempFiles, string[] filePaths)
    {
        var newFiles = await _mediaService.SaveToTempAsync(filePaths);
        int skipped = 0;

        foreach (var file in newFiles)
        {
            if (file is null || string.IsNullOrEmpty(file.ContentHash)) continue;

            // Защита от дубля: такой же файл уже есть во временных файлах
            if (tempFiles.Any(t => BelongsToSameMedia(t, file)))
            {
                skipped++;
                _mediaService.DeleteTempFiles(new[] { file }); // удаляем временную копию дубля
                continue;
            }

            // Проверяем, существует ли такой файл уже в БД (по ContentHash).
            // Если да — устанавливаем MediaId, чтобы файл помечался как уже добавленный
            // и не дублировался при повторном открытии диалога выбора медиа.
            var existingMediaId = await _mediaService.GetMediaIdByContentHashAsync(file.ContentHash);
            if (existingMediaId.HasValue)
            {
                file.MediaId = existingMediaId.Value;
            }

            tempFiles.Add(file);
        }

        if (skipped > 0)
        {
            Notification.Show("Внимание",
                $"{skipped} уже добавлен",
                NotificationType.Warning);
        }
    }

    /// Удаляет временный файл из коллекции и диска
    public void RemoveTempFile(ObservableCollection<MediaItemViewModel> tempFiles, MediaItemViewModel item)
    {
        if (item == null) return;
        tempFiles.Remove(item);
        _mediaService.DeleteTempFiles(new[] { item });
    }
}
