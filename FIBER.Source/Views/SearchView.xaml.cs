using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;
using UserControl = System.Windows.Controls.UserControl;

namespace FIBER.Source.Views;

public partial class SearchView : UserControl
{
    public SearchView() => InitializeComponent();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SearchViewModel oldVm)
        {
            oldVm.DuplicatesRequested -= OnDuplicatesRequested;
            oldVm.ExportRequested -= OnExportRequested;
            oldVm.ImageZoomRequested -= OnImageZoomRequested;
            oldVm.VideoZoomRequested -= OnVideoZoomRequested;
        }

        if (e.NewValue is SearchViewModel newVm)
        {
            newVm.DuplicatesRequested += OnDuplicatesRequested;
            newVm.ExportRequested += OnExportRequested;
            newVm.ImageZoomRequested += OnImageZoomRequested;
            newVm.VideoZoomRequested += OnVideoZoomRequested;
        }
    }

    private void OnDuplicatesRequested(int personId)
    {
        var services = ((App)Application.Current).Services;
        if (services is null) return;

        var duplicateFinderService = services.GetRequiredService<IDuplicateFinderService>();
        var window = new DuplicateFinderWindow(personId, duplicateFinderService)
        {
            Owner = Window.GetWindow(this)
        };
        window.NavigateRequested += ((SearchViewModel)DataContext)!.NavigateToDuplicate;
        window.ShowDialog();
    }

    private async void OnExportRequested(MediaOwnerType ownerType, int ownerId, string title)
    {
        var services = ((App)Application.Current).Services;
        if (services is null) return;

        var exportService = services.GetRequiredService<IExportService>();
        var mediaService = services.GetRequiredService<IMediaService>();

        List<MediaItemViewModel> media;
        try
        {
            media = (await mediaService.GetMediaForOwnerAsync(ownerType, ownerId))
                .Select(dto => dto.ToViewModel(mediaService))
                .ToList();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Не удалось загрузить медиа: {ex.Message}", NotificationType.Error);
            return;
        }

        try
        {
            var dialog = new ExportDialogWindow(title, media)
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;

            string folder;
            switch (ownerType)
            {
                case MediaOwnerType.Person:
                    folder = await exportService.ExportPersonAsync(
                        ownerId, dialog.TargetFolder, dialog.SelectedFormat, dialog.SelectedMediaIds);
                    break;
                case MediaOwnerType.Company:
                    folder = await exportService.ExportCompanyAsync(
                        ownerId, dialog.TargetFolder, dialog.SelectedFormat, dialog.SelectedMediaIds);
                    break;
                case MediaOwnerType.Event:
                    folder = await exportService.ExportEventAsync(
                        ownerId, dialog.TargetFolder, dialog.SelectedFormat, dialog.SelectedMediaIds);
                    break;
                default:
                    return;
            }

            Notification.Show("Экспорт завершён",
                $"Сохранено в:\n{folder}",
                NotificationType.Success);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка экспорта", ex.Message, NotificationType.Error);
        }
        finally
        {
            foreach (var m in media) m.Dispose();
        }
    }

    private void OnImageZoomRequested(MediaItemViewModel mediaVm)
    {
        if (mediaVm == null || mediaVm.ImageSource == null) return;
        
        // Determine which collection this media belongs to
        var source = new List<MediaItemViewModel>();
        
        var vm = DataContext as SearchViewModel;
        if (vm != null)
        {
            if (vm.PersonImages.Contains(mediaVm))
                source = vm.PersonImages.ToList();
            else if (vm.CompanyImages.Contains(mediaVm))
                source = vm.CompanyImages.ToList();
            else if (vm.EventImages.Contains(mediaVm))
                source = vm.EventImages.ToList();
        }
        
        if (source.Count == 0)
        {
            source.Add(mediaVm);
        }
        
        int idx = source.IndexOf(mediaVm);
        if (idx < 0) idx = 0;
        
        var zoomWindow = new ImageZoomWindow(source, idx);
        var owner = Window.GetWindow(this);
        if (owner != null) zoomWindow.Owner = owner;
        zoomWindow.ShowDialog();
    }

    private void OnVideoZoomRequested(MediaItemViewModel mediaVm)
    {
        if (mediaVm == null || string.IsNullOrEmpty(mediaVm.FilePath)) return;

        var source = new List<MediaItemViewModel>();

        var vm = DataContext as SearchViewModel;
        if (vm != null)
        {
            if (vm.PersonVideos.Contains(mediaVm))
                source = vm.PersonVideos.ToList();
            else if (vm.CompanyVideos.Contains(mediaVm))
                source = vm.CompanyVideos.ToList();
            else if (vm.EventVideos.Contains(mediaVm))
                source = vm.EventVideos.ToList();
        }

        if (source.Count == 0)
        {
            source.Add(mediaVm);
        }

        int idx = source.IndexOf(mediaVm);
        if (idx < 0) idx = 0;

        var zoomWindow = new VideoZoomWindow(source, idx);
        var owner = Window.GetWindow(this);
        if (owner != null) zoomWindow.Owner = owner;
        zoomWindow.ShowDialog();
    }
}
