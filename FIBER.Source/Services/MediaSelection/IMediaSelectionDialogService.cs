using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Services.MediaSelection;

/// <summary>Абстракция диалога выбора существующих медиафайлов.</summary>
public interface IMediaSelectionDialogService
{
    /// <summary>Показывает диалог выбора медиа и возвращает Id выбранных (пусто — отмена/ничего не выбрано).</summary>
    Task<IReadOnlyList<int>> ShowSelectMediaAsync(IEnumerable<MediaItemViewModel> availableMedia);
}
