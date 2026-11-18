using FIBER.Source.Contracts;
using FIBER.Source.Interfaces;
using System;

namespace FIBER.Source.ViewModels;

// Методы расширения для конвертации между MediaItemDto (сервисный слой) и MediaItemViewModel (слой представления)
public static class MediaDtoExtensions
{
    /// Закрывает ресурсы UI до асинхронного удаления снимка временных файлов
    public static async Task ClearTempFilesAsync(
        this System.Collections.ObjectModel.ObservableCollection<MediaItemViewModel> items, IMediaService service)
    {
        var files = items.Select(item => item.ToDto()).ToArray();
        foreach (var item in items) item.Dispose();
        items.Clear();
        await service.DeleteTempFilesAsync(files);
    }

    // Конвертирует DTO в VM
    // для медиа из БД (MediaId задан) — ленивую загрузку через IMediaService
    public static MediaItemViewModel ToViewModel(this MediaItemDto dto, IMediaService? mediaService = null)
    {
        if (!string.IsNullOrEmpty(dto.FilePath))
        {
            var vm = new MediaItemViewModel(dto.FilePath, dto.OriginalFileName) { DeleteOnDispose = true };
            if (dto.MediaId is int id) vm.MediaId = id;
            if (!string.IsNullOrEmpty(dto.ContentHash)) vm.ContentHash = dto.ContentHash;
            return vm;
        }

        if (dto.MediaId is int mediaId && mediaService is not null)
        {
            return new MediaItemViewModel(mediaService, mediaId, dto.OriginalFileName)
            {
                ContentHash = dto.ContentHash ?? string.Empty
            };
        }

        return new MediaItemViewModel(string.Empty, dto.OriginalFileName)
        {
            MediaId = dto.MediaId,
            ContentHash = dto.ContentHash ?? string.Empty
        };
    }

    public static MediaItemDto ToDto(this MediaItemViewModel vm)
    {
        return new MediaItemDto(
            MediaId: vm.MediaId,
            FilePath: vm.FilePath,
            OriginalFileName: vm.FileName,
            MimeType: string.Empty,
            SizeBytes: 0,
            ContentHash: vm.ContentHash ?? string.Empty,
            IsImage: vm.IsImage,
            IsVideo: vm.IsVideo,
            IsAudio: vm.IsAudio);
    }
}
