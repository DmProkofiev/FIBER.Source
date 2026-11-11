using FIBER.Source.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FIBER.Source.Services.MediaSelection;

/// Абстракция диалога выбора существующих медиафайлов
public interface IMediaSelectionDialogService
{
    /// Показывает диалог выбора медиа и возвращает Id выбранных пусто — отмена/ничего не выбрано
    Task<IReadOnlyList<int>> ShowSelectMediaAsync(IEnumerable<MediaItemDto> availableMedia, IReadOnlyCollection<int> alreadyAddedMediaIds);
}
