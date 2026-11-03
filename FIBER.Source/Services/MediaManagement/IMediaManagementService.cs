using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FIBER.Source.Services.MediaManagement;

/// <summary>Результат загрузки существующих медиа владельца, сгруппированный по типам.</summary>
public sealed class OwnerMedia
{
    public ObservableCollection<MediaItemViewModel> Images { get; } = new();
    public ObservableCollection<MediaItemViewModel> Videos { get; } = new();
    public ObservableCollection<MediaItemViewModel> Audios { get; } = new();

    public int TotalCount => Images.Count + Videos.Count + Audios.Count;
}

/// <summary>Операции над медиа, общие для всех Update-экранов (человек, событие, организация).</summary>
public interface IMediaManagementService
{
    /// <summary>Загружает существующие медиа владельца, сгруппированные по типам.</summary>
    Task<OwnerMedia> LoadOwnerMediaAsync(MediaOwnerType ownerType, int ownerId);

    /// <summary>Очищает коллекции существующих медиа с утилизацией ресурсов.</summary>
    void ClearMedia(OwnerMedia media);

    /// <summary>Добавляет существующий медиафайл к владельцу через диалог выбора.</summary>
    Task AddExistingMediaAsync(MediaOwnerType ownerType, int ownerId);

    /// <summary>Удаляет существующий медиафайл у владельца.</summary>
    Task DeleteExistingMediaAsync(MediaOwnerType ownerType, int ownerId, MediaItemViewModel media);

    /// <summary>Обрабатывает перетаскивание файлов (добавляет во временные файлы).</summary>
    Task HandleFilesDropAsync(ObservableCollection<MediaItemViewModel> tempFiles, string[] filePaths);

    /// <summary>Удаляет временный файл из коллекции и диска.</summary>
    void RemoveTempFile(ObservableCollection<MediaItemViewModel> tempFiles, MediaItemViewModel item);
}
