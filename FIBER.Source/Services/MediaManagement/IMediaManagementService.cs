using FIBER.Source.Contracts;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FIBER.Source.Services.MediaManagement;

/// Результат загрузки существующих медиа владельца, сгруппированный по типам
public sealed class OwnerMedia
{
    public ObservableCollection<MediaItemViewModel> Images { get; } = new();
    public ObservableCollection<MediaItemViewModel> Videos { get; } = new();
    public ObservableCollection<MediaItemViewModel> Audios { get; } = new();

    public int TotalCount => Images.Count + Videos.Count + Audios.Count;
}

/// Операции над медиа, общие для всех Update-экранов (человек, событие, организация).
/// Сервис не показывает UI-уведомлений — только возвращает результаты или выбрасывает исключения.
/// Для передачи данных используются чистые DTO (MediaItemDto), а MediaItemViewModel
/// используется только для манипуляции существующими UI-коллекциями.
public interface IMediaManagementService
{
    /// Загружает существующие медиа владельца, сгруппированные по типам
    Task<OwnerMedia> LoadOwnerMediaAsync(MediaOwnerType ownerType, int ownerId);

    /// Очищает коллекции существующих медиа с утилизацией ресурсов VM
    void ClearMedia(OwnerMedia media);

    /// Открывает диалог выбора существующих медиафайлов и возвращает выбранные.
    /// Пустой список — диалог отменён или ничего не выбрано (или в базе нет медиа).
    Task<IReadOnlyList<MediaItemDto>> SelectExistingMediaAsync(IReadOnlyCollection<int>? alreadyAddedMediaIds = null);

    /// Открывает диалог выбора существующих медиафайлов для владельца.
    Task<IReadOnlyList<MediaItemDto>> SelectExistingMediaAsync(MediaOwnerType ownerType, int ownerId,
        IReadOnlyCollection<int>? alreadyAddedMediaIds = null);

    /// Добавляет медиа во временные файлы с защитой от дублей.
    /// Возвращает количество добавленных файлов.
    Task<int> AddToTempFilesAsync(ObservableCollection<MediaItemViewModel> tempFiles, IEnumerable<MediaItemDto> media);

    /// Привязывает существующие медиафайлы (по MediaId) к владельцу в БД.
    /// Возвращает число пропущенных (уже привязанных) файлов.
    Task<int> LinkExistingMediaAsync(IEnumerable<int> mediaIds, MediaOwnerType ownerType, int ownerId);

    /// Удаляет существующий медиафайл у владельца.
    /// Возвращает true, если файл удалён.
    Task<bool> DeleteExistingMediaAsync(MediaOwnerType ownerType, int ownerId, MediaItemDto media);

    /// Обрабатывает перетаскивание файлов (добавляет во временные файлы).
    /// Возвращает количество пропущенных дублей.
    Task<int> HandleFilesDropAsync(ObservableCollection<MediaItemViewModel> tempFiles, string[] filePaths);

    /// Удаляет временный файл из коллекции и диска
    void RemoveTempFile(ObservableCollection<MediaItemViewModel> tempFiles, MediaItemViewModel item);
}
