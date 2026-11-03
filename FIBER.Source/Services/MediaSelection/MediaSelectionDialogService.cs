using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;
using FIBER.Source.Views;
using Application = System.Windows.Application;

namespace FIBER.Source.Services.MediaSelection;

/// <summary>Реализация диалога выбора медиа: открывает MediaSelectionWindow.</summary>
public sealed class MediaSelectionDialogService : IMediaSelectionDialogService
{
    /// <summary>Показывает диалог выбора медиа и возвращает Id выбранных.</summary>
    public Task<IReadOnlyList<int>> ShowSelectMediaAsync(IEnumerable<MediaItemViewModel> availableMedia)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return Task.FromException<IReadOnlyList<int>>(new InvalidOperationException("Application.Current is not available"));

        var tcs = new TaskCompletionSource<IReadOnlyList<int>>();

        dispatcher.Invoke(() =>
        {
            try
            {
                var dialog = new MediaSelectionWindow("Добавить медиафайл", availableMedia);
                var owner = Application.Current?.MainWindow;
                if (owner is not null && owner.IsVisible && !ReferenceEquals(owner, dialog))
                    dialog.Owner = owner!;

                bool? result = dialog.ShowDialog();
                if (result == true)
                    tcs.SetResult(dialog.SelectedMediaIds);
                else
                    tcs.SetResult(Array.Empty<int>());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}
