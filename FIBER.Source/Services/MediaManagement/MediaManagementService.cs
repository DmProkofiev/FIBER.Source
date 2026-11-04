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

/// <summary>Реализация общих операций с медиа для Update-экранов.</summary>
public sealed class MediaManagementService : IMediaManagementService
{
    private readonly IMediaService _mediaService;
    private readonly IMediaSelectionDialogService _dialogService;

    public MediaManagementService(IMediaService mediaService, IMediaSelectionDialogService dialogService)
    {
        _mediaService = mediaService;
        _dialogService = dialogService;
    }

    /// <summary>Загружает существующие медиа владельца, сгруппированные по типам.</summary>
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

    /// <summary>Очищает коллекции существующих медиа.
    /// Не утилизирует VM — они belong к общему кэшу MediaService и используются повторно.</summary>
    public void ClearMedia(OwnerMedia media)
    {
        media.Images.Clear();
        media.Videos.Clear();
        media.Audios.Clear();
    }

    /// <summary>Открывает диалог выбора существующих медиафайлов и возвращает выбранные.
    /// Возвращает выбранные медиа для добавления во TempFiles (ручное добавление),
    /// а не прямую привязку к владельцу в БД.
    /// Пустой список — диалог отменён или ничего не выбрано.</summary>
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

    /// <summary>Удаляет существующий медиафайл у владельца.</summary>
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

    /// <summary>Обрабатывает перетаскивание файлов (добавляет во временные файлы).</summary>
    public async Task HandleFilesDropAsync(ObservableCollection<MediaItemViewModel> tempFiles, string[] filePaths)
    {
        var newFiles = await _mediaService.SaveToTempAsync(filePaths);
        foreach (var file in newFiles) tempFiles.Add(file);
    }

    /// <summary>Удаляет временный файл из коллекции и диска.</summary>
    public void RemoveTempFile(ObservableCollection<MediaItemViewModel> tempFiles, MediaItemViewModel item)
    {
        if (item == null) return;
        tempFiles.Remove(item);
        _mediaService.DeleteTempFiles(new[] { item });
    }
}
