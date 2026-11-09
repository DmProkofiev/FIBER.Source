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

/// Реализация диалога выбора медиа: открывает MediaSelectionWindow
public sealed class MediaSelectionDialogService : IMediaSelectionDialogService
{
    public Task<IReadOnlyList<int>> ShowSelectMediaAsync(IEnumerable<MediaItemViewModel> availableMedia)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<int>>();
        var app = Application.Current;
        var dispatcher = app?.Dispatcher;
        if (dispatcher is null)
        {
            tcs.SetException(new InvalidOperationException("Application.Current.Dispatcher is not available"));
            return tcs.Task;
        }

        var owner = app!.MainWindow;

        dispatcher.BeginInvoke(() =>
        {
            try
            {
                var dialog = new MediaSelectionWindow("Добавить медиафайл", availableMedia)
                {
                    Owner = owner
                };
                tcs.SetResult(dialog.ShowDialog() == true
                    ? dialog.SelectedMediaIds
                    : Array.Empty<int>());
            }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }
}
