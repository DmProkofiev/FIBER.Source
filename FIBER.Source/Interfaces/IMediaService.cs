using System.Collections.Generic;
using System.Threading.Tasks;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Interfaces;

/// <summary>Запись для списка «все медиа» с информацией о владельце.</summary>
public sealed record MediaForOwner(MediaItemViewModel Media, MediaOwnerType OwnerType, int OwnerId);

/// <summary>
/// Контракт сервиса медиа-вложений, хранимых в виде BLOB в зашифрованной БД.
/// </summary>
public interface IMediaService
{
    /// <summary>Копирует выбранные файлы во временный каталог %Temp%\FIBER\ для предпросмотра.</summary>
    Task<List<MediaItemViewModel>> SaveToTempAsync(string[] filePaths);

    /// <summary>Фиксирует временные файлы в БД: SHA-256, дедупликация, M2M-связь.</summary>
    Task CommitTempFilesAsync(List<MediaItemViewModel> tempFiles, MediaOwnerType ownerType, int ownerId);

    /// <summary>Удаляет временные файлы из Temp.</summary>
    void DeleteTempFiles(IEnumerable<MediaItemViewModel> tempFiles);

    /// <summary>Возвращает медиа владельца: изображения из памяти, видео/аудио через Temp-файл.</summary>
    Task<List<MediaItemViewModel>> GetMediaForOwnerAsync(MediaOwnerType ownerType, int ownerId);

    /// <summary>Возвращает все медиа со всех владельцев.</summary>
    Task<IReadOnlyList<MediaForOwner>> GetAllMediaAsync();

    /// <summary>Удаляет одну связь; при отсутствии других ссылок удаляет BLOB и запись.</summary>
    Task RemoveLinkAsync(int mediaId, MediaOwnerType ownerType, int ownerId);

    /// <summary>Удаляет все связи владельца и подчищает осиротевшие MediaAttachment.</summary>
    Task RemoveAllLinksForOwnerAsync(MediaOwnerType ownerType, int ownerId);
}