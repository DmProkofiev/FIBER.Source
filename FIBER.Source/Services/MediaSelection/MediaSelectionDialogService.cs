using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FIBER.Source.Contracts;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;
using FIBER.Source.Views;
using Application = System.Windows.Application;

namespace FIBER.Source.Services.MediaSelection;

/// Реализация диалога выбора медиа: открывает MediaSelectionWindow
public sealed class MediaSelectionDialogService : IMediaSelectionDialogService
{
    private readonly IMediaService _mediaService;

    public MediaSelectionDialogService(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    public async Task<IReadOnlyList<int>> ShowSelectMediaAsync(IEnumerable<MediaItemDto> availableMedia, IReadOnlyCollection<int> alreadyAddedMediaIds)
    {
        var app = Application.Current;
        var dispatcher = app?.Dispatcher
            ?? throw new InvalidOperationException("Application.Current.Dispatcher is not available");
        var items = availableMedia.ToArray();
        return await dispatcher.InvokeAsync<IReadOnlyList<int>>(() =>
        {
            var vms = items.Select(dto => dto.ToViewModel(_mediaService)).ToList();
            try
            {
                var dialog = new MediaSelectionWindow("Добавить медиафайл", vms, alreadyAddedMediaIds)
                {
                    Owner = app!.MainWindow
                };
                return dialog.ShowDialog() == true
                    ? dialog.SelectedMediaIds
                    : Array.Empty<int>();
            }
            finally { foreach (var vm in vms) vm.Dispose(); }
        });
    }
}
