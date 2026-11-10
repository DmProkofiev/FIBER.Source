using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Services.MediaSelection;

/// Абстракция диалога выбора существующих медиафайлов
public interface IMediaSelectionDialogService
{
    /// Показывает диалог выбора медиа и возвращает Id выбранных пусто — отмена/ничего не выбрано
    Task<IReadOnlyList<int>> ShowSelectMediaAsync(IEnumerable<MediaItemViewModel> availableMedia, IReadOnlyCollection<int> alreadyAddedMediaIds);
}
