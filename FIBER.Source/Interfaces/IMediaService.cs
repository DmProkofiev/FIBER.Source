using System.Collections.Generic;
using System.Threading.Tasks;
using FIBER.Source.Contracts;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Interfaces;

/// <summary>Запись для списка «все медиа» с информацией о владельце.</summary>
public sealed record MediaForOwner(MediaItemDto Media, MediaOwnerType OwnerType, int OwnerId);

/// <summary>
/// Контракт сервиса медиа-вложений, хранимых в виде BLOB в зашифрованной БД.
/// Сервис не зависит от слоя представления — возвращает чистые DTO.
/// </summary>
public interface IMediaService
{
    /// <summary>Копирует выбранные файлы во временный каталог %Temp%\FIBER\ для предпросмотра.</summary>
    Task<List<MediaItemDto>> SaveToTempAsync(string[] filePaths);

    /// <summary>Фиксирует временные файлы в БД: SHA-256, дедупликация, M2M-связь.</summary>
    Task CommitTempFilesAsync(List<MediaItemDto> tempFiles, MediaOwnerType ownerType, int ownerId);

    /// <summary>Удаляет временные файлы из Temp.</summary>
    void DeleteTempFiles(IEnumerable<MediaItemDto> tempFiles);

    /// <summary>Добавляет существующий медиафайл к владельцу (без дублирования ссылки).</summary>
    Task AddLinkAsync(int mediaId, MediaOwnerType ownerType, int ownerId);

    /// <summary>Проверяет, привязан ли медиафайл уже к данному владельцу.</summary>
    Task<bool> IsMediaLinkedToOwnerAsync(int mediaId, MediaOwnerType ownerType, int ownerId);

    /// <summary>Возвращает медиа владельца.</summary>
    Task<List<MediaItemDto>> GetMediaForOwnerAsync(MediaOwnerType ownerType, int ownerId);

    /// <summary>Возвращает все медиа со всех владельцев.</summary>
    Task<IReadOnlyList<MediaForOwner>> GetAllMediaAsync();

    /// <summary>Удаляет одну связь; при отсутствии других ссылок удаляет BLOB и запись.</summary>
    Task RemoveLinkAsync(int mediaId, MediaOwnerType ownerType, int ownerId);

    /// <summary>Удаляет все связи владельца и подчищает осиротевшие MediaAttachment.</summary>
    Task RemoveAllLinksForOwnerAsync(MediaOwnerType ownerType, int ownerId);

    /// <summary>Читает BLOB-данные вложения по идентификатору (для ленивой загрузки).</summary>
    Task<byte[]?> GetMediaDataAsync(int mediaId);

    /// <summary>Вычисляет SHA-256 hex содержимого файла (для защиты от дублей).</summary>
    Task<string> ComputeFileHashAsync(string filePath);

    /// <summary>Ищет MediaId по ContentHash в БД. Возвращает null, если файл не найден.</summary>
    Task<int?> GetMediaIdByContentHashAsync(string contentHash);

    /// <summary>Инвалидировать кэш списка «все медиа» (вызывать после изменения ссылок).</summary>
    void InvalidateAllMediaCache();

    /// <summary>Предзагружает все медиа в кэш при запуске приложения (один раз), чтобы при нажатии «Добавить» не было зависания.</summary>
    Task PreloadAllMediaAsync();
}
