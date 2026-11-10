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
    /// alreadyAddedMediaIds — медиа, уже добавленные в TempFiles (для Create-экранов),
    /// они будут помечены галочкой и исключены из результата выбора.
    Task<IReadOnlyList<MediaItemViewModel>> SelectExistingMediaAsync(IReadOnlyCollection<int>? alreadyAddedMediaIds = null);

    /// Открывает диалог выбора существующих медиафайлов для владельца:
    /// уже добавленные к владельцу файлы помечаются галочкой и не подлежат повторному добавлению.
    /// alreadyAddedMediaIds — медиа, уже добавленные в TempFiles (Drag&Drop или предыдущий вызов),
    /// они также будут помечены галочкой и исключены из результата выбора.
    Task<IReadOnlyList<MediaItemViewModel>> SelectExistingMediaAsync(MediaOwnerType ownerType, int ownerId,
        IReadOnlyCollection<int>? alreadyAddedMediaIds = null);

    /// Добавляет медиа во временные файлы с защитой от дублей
    Task AddToTempFilesAsync(ObservableCollection<MediaItemViewModel> tempFiles, IEnumerable<MediaItemViewModel> media);

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
