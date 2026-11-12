using FIBER.Source.Contracts;
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
        var dtoList = await _mediaService.GetMediaForOwnerAsync(ownerType, ownerId);

        foreach (var dto in dtoList)
        {
            var vm = DtoToVm(dto);
            if (dto.IsImage) media.Images.Add(vm);
            else if (dto.IsVideo) media.Videos.Add(vm);
            else if (dto.IsAudio) media.Audios.Add(vm);

            if (dto.IsImage)
                _ = vm.LoadImageAsync();
        }

        return media;
    }

    /// Очищает коллекции существующих медиа с утилизацией ресурсов VM
    public void ClearMedia(OwnerMedia media)
    {
        foreach (var vm in media.Images) vm.Dispose();
        foreach (var vm in media.Videos) vm.Dispose();
        foreach (var vm in media.Audios) vm.Dispose();
        media.Images.Clear();
        media.Videos.Clear();
        media.Audios.Clear();
    }

    /// Открывает диалог выбора существующих медиафайлов и возвращает выбранные
    public async Task<IReadOnlyList<MediaItemDto>> SelectExistingMediaAsync(IReadOnlyCollection<int>? alreadyAddedMediaIds = null)
    {
        var allMedia = await _mediaService.GetAllMediaAsync();
        var items = allMedia.Select(m => m.Media).ToList();

        if (items.Count == 0)
            return Array.Empty<MediaItemDto>();

        var selectedIds = await _dialogService.ShowSelectMediaAsync(items, alreadyAddedMediaIds ?? Array.Empty<int>());
        if (selectedIds.Count == 0) return Array.Empty<MediaItemDto>();

        return items.Where(m => selectedIds.Contains(m.MediaId ?? 0)).ToList();
    }

    /// <summary>Открывает диалог выбора существующих медиафайлов для владельца.</summary>
    public async Task<IReadOnlyList<MediaItemDto>> SelectExistingMediaAsync(MediaOwnerType ownerType, int ownerId,
        IReadOnlyCollection<int>? alreadyAddedMediaIds = null)
    {
        var allMedia = await _mediaService.GetAllMediaAsync();
        var items = allMedia.Select(m => m.Media).ToList();

        if (items.Count == 0)
            return Array.Empty<MediaItemDto>();

        var alreadyAdded = new HashSet<int>();
        var ownerMedia = await _mediaService.GetMediaForOwnerAsync(ownerType, ownerId);
        foreach (var dto in ownerMedia)
        {
            if (dto.MediaId is int id)
                alreadyAdded.Add(id);
        }
        if (alreadyAddedMediaIds != null)
        {
            foreach (var id in alreadyAddedMediaIds)
                alreadyAdded.Add(id);
        }

        var selectedIds = await _dialogService.ShowSelectMediaAsync(items, alreadyAdded);
        if (selectedIds.Count == 0) return Array.Empty<MediaItemDto>();

        return items.Where(m => selectedIds.Contains(m.MediaId ?? 0)).ToList();
    }

    /// <summary>Добавляет медиа во временные файлы с защитой от дублей.
    /// Возвращает количество добавленных файлов.</summary>
    public async Task<int> AddToTempFilesAsync(ObservableCollection<MediaItemViewModel> tempFiles, IEnumerable<MediaItemDto> media)
    {
        var list = media?.Where(m => m is not null).ToList() ?? new List<MediaItemDto>();
        int added = 0;
        foreach (var dto in list)
        {
            if (tempFiles.Any(t => BelongsToSameMedia(t, dto))) continue;
            tempFiles.Add(DtoToVm(dto));
            added++;
        }
        return added;
    }

    /// <summary>Проверка «один и тот же медиа»: совпадение по идентификатору, контенту или пути к файлу.</summary>
    private static bool BelongsToSameMedia(MediaItemViewModel a, MediaItemDto b)
    {
        if (a is null || b is null) return false;

        // По идентификатору вложения в БД
        if (a.MediaId.HasValue && b.MediaId.HasValue && a.MediaId == b.MediaId.Value) return true;

        // По SHA-256 содержимому (для файлов без MediaId, загруженных через Drag&Drop)
        if (!string.IsNullOrEmpty(a.ContentHash) && !string.IsNullOrEmpty(b.ContentHash)
            && a.ContentHash == b.ContentHash) return true;

        // По пути к Temp-файлу (для файлов, скопированных из одного и того же источника)
        if (!string.IsNullOrEmpty(a.FilePath) && !string.IsNullOrEmpty(b.FilePath)
            && string.Equals(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    /// Привязывает существующие медиафайлы (по MediaId) к владельцу в БД.
    /// Возвращает число пропущенных (уже привязанных) файлов.
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

    /// Удаляет существующий медиафайл у владельца.
    /// Возвращает true, если файл удалён.
    public async Task<bool> DeleteExistingMediaAsync(MediaOwnerType ownerType, int ownerId, MediaItemDto media)
    {
        if (media.MediaId is null) return false;

        try
        {
            await _mediaService.RemoveLinkAsync(media.MediaId.Value, ownerType, ownerId);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Ошибка удаления медиафайла: {ex.Message}", ex);
        }
    }

    /// Обрабатывает перетаскивание файлов (добавляет во временные файлы).
    public async Task<int> HandleFilesDropAsync(ObservableCollection<MediaItemViewModel> tempFiles, string[] filePaths)
    {
        var newFiles = await _mediaService.SaveToTempAsync(filePaths);
        int skipped = 0;

        foreach (var dto in newFiles)
        {
            if (dto is null || string.IsNullOrEmpty(dto.ContentHash)) continue;

            // Защита от дубля: такой же файл уже есть во временных файлах
            if (tempFiles.Any(t => BelongsToSameMedia(t, dto)))
            {
                skipped++;
                _mediaService.DeleteTempFiles(new[] { dto }); // удаляем временную копию дубля
                continue;
            }

            var existingMediaId = await _mediaService.GetMediaIdByContentHashAsync(dto.ContentHash);
            if (existingMediaId.HasValue)
            {
                var vm = DtoToVm(dto);
                vm.MediaId = existingMediaId.Value;
                tempFiles.Add(vm);
            }
            else
            {
                tempFiles.Add(DtoToVm(dto));
            }
        }

        return skipped;
    }

    /// Удаляет временный файл из коллекции и диска
    public void RemoveTempFile(ObservableCollection<MediaItemViewModel> tempFiles, MediaItemViewModel item)
    {
        if (item == null) return;
        tempFiles.Remove(item);
        _mediaService.DeleteTempFiles(new[] { item.ToDto() });
    }

    /// <summary>Конвертирует DTO в VM.</summary>
    private MediaItemViewModel DtoToVm(MediaItemDto dto)
    {
        if (!string.IsNullOrEmpty(dto.FilePath))
        {
            var vm = new MediaItemViewModel(dto.FilePath, dto.OriginalFileName);
            if (dto.MediaId is int id) vm.MediaId = id;
            if (!string.IsNullOrEmpty(dto.ContentHash)) vm.ContentHash = dto.ContentHash;
            return vm;
        }

        // Ленивая загрузка из БД через IMediaService.
        if (dto.MediaId is int mediaId)
        {
            return new MediaItemViewModel(_mediaService, mediaId, dto.OriginalFileName)
            {
                ContentHash = dto.ContentHash ?? string.Empty
            };
        }

        return new MediaItemViewModel(string.Empty, dto.OriginalFileName);
    }
}
