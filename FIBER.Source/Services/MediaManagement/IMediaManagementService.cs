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

/// Операции над медиа, общие для всех Update-экранов (человек, событие, организация)
public interface IMediaManagementService
{
    /// Загружает существующие медиа владельца, сгруппированные по типам
    Task<OwnerMedia> LoadOwnerMediaAsync(MediaOwnerType ownerType, int ownerId);

    /// Очищает коллекции существующих медиа с утилизацией ресурсов
    void ClearMedia(OwnerMedia media);

    /// Открывает диалог выбора существующих медиафайлов и возвращает выбранные
    /// Возвращает выбранные медиа-элементы (уже имеют MediaId, файл кэширован во Temp)
    /// Пустой список — диалог отменён или ничего не выбрано
    Task<IReadOnlyList<MediaItemViewModel>> SelectExistingMediaAsync();

    /// Привязывает существующие медиафайлы (по MediaId) к владельцу в БД.
    /// Уже привязанные к владельцу файлы пропускаются (без дублей).
    /// Возвращает число пропущенных (уже привязанных) файлов — для уведомления пользователя
    Task<int> LinkExistingMediaAsync(IEnumerable<int> mediaIds, MediaOwnerType ownerType, int ownerId);

    /// Удаляет существующий медиафайл у владельца
    Task DeleteExistingMediaAsync(MediaOwnerType ownerType, int ownerId, MediaItemViewModel media);

    /// Обрабатывает перетаскивание файлов (добавляет во временные файлы)
    Task HandleFilesDropAsync(ObservableCollection<MediaItemViewModel> tempFiles, string[] filePaths);

    /// Удаляет временный файл из коллекции и диска
    void RemoveTempFile(ObservableCollection<MediaItemViewModel> tempFiles, MediaItemViewModel item);
}
