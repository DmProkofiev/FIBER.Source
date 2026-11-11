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

    public Task<IReadOnlyList<int>> ShowSelectMediaAsync(IEnumerable<MediaItemDto> availableMedia, IReadOnlyCollection<int> alreadyAddedMediaIds)
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

        // Конвертируем DTO в VM для отображения в окне выбора медиа.
        // Temp-файлы (FilePath не пуст) — создаём VM из пути к файлу.
        // Медиа из БД (MediaId задан, FilePath пуст) — ленивая загрузка через IMediaService.
        var vms = availableMedia.Select(dto =>
        {
            MediaItemViewModel vm;
            if (!string.IsNullOrEmpty(dto.FilePath))
            {
                vm = new MediaItemViewModel(dto.FilePath, dto.OriginalFileName);
                if (dto.MediaId is int id) vm.MediaId = id;
                if (!string.IsNullOrEmpty(dto.ContentHash)) vm.ContentHash = dto.ContentHash;
            }
            else if (dto.MediaId is int mediaId)
            {
                vm = new MediaItemViewModel(_mediaService, mediaId, dto.OriginalFileName);
                if (!string.IsNullOrEmpty(dto.ContentHash)) vm.ContentHash = dto.ContentHash;
            }
            else
            {
                vm = new MediaItemViewModel(string.Empty, dto.OriginalFileName);
                if (dto.MediaId is int id2) vm.MediaId = id2;
                if (!string.IsNullOrEmpty(dto.ContentHash)) vm.ContentHash = dto.ContentHash;
            }
            return vm;
        }).ToList();

        dispatcher.BeginInvoke(() =>
        {
            try
            {
                var dialog = new MediaSelectionWindow("Добавить медиафайл", vms, alreadyAddedMediaIds)
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
