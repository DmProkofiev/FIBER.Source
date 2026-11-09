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

    /// Открывает диалог выбора существующих медиафайлов и возвращает выбранные.
    /// Возвращает выбранные медиа для добавления во TempFiles (ручное добавление),
    /// а не прямую привязку к владельцу в БД.
    /// Пустой список — диалог отменён или ничего не выбрано
    public async Task<IReadOnlyList<MediaItemViewModel>> SelectExistingMediaAsync()
    {
        var allMedia = await _mediaService.GetAllMediaAsync();
        var items = allMedia.Where(m => m.Media is not null).Select(m => m.Media!).ToList();

        if (items.Count == 0)
        {
            Notification.Show("Медиа", "В базе нет медиафайлов", FIBER.Source.Helpers.NotificationType.Info);
            return Array.Empty<MediaItemViewModel>();
        }

        var selectedIds = await _dialogService.ShowSelectMediaAsync(items);
        if (selectedIds.Count == 0) return Array.Empty<MediaItemViewModel>();

        // Возвращаем выбранные медиа-элементы для добавления во TempFiles ViewModel
        return items.Where(m => selectedIds.Contains(m.MediaId ?? 0)).ToList();
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
    /// перетащен во временные файлы более одного раз
    public async Task HandleFilesDropAsync(ObservableCollection<MediaItemViewModel> tempFiles, string[] filePaths)
    {
        var newFiles = await _mediaService.SaveToTempAsync(filePaths);
        int skipped = 0;

        foreach (var file in newFiles)
        {
            if (file is null || string.IsNullOrEmpty(file.ContentHash)) continue;

            // Защита от дубля: такой же файл уже есть во временных файлах
            if (tempFiles.Any(t => t.ContentHash == file.ContentHash))
            {
                skipped++;
                _mediaService.DeleteTempFiles(new[] { file }); // удаляем временную копию дубля
                continue;
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
