using FIBER.Source.Contracts;
using FIBER.Source.Interfaces;
using System;

namespace FIBER.Source.ViewModels;

/// <summary>Методы расширения для конвертации между MediaItemDto (сервисный слой) и MediaItemViewModel (слой представления).</summary>
public static class MediaDtoExtensions
{
    /// <summary>Конвертирует DTO в VM. Для Temp-файлов (FilePath не пуст) создаёт VM из пути к файлу;
    /// для медиа из БД (MediaId задан) — ленивую загрузку через IMediaService.</summary>
    public static MediaItemViewModel ToViewModel(this MediaItemDto dto, IMediaService? mediaService = null)
    {
        if (!string.IsNullOrEmpty(dto.FilePath))
        {
            var vm = new MediaItemViewModel(dto.FilePath, dto.OriginalFileName);
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

    /// <summary>Конвертирует VM в DTO для передачи в сервисный слой.</summary>
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
