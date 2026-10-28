using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

    // Открывает окно предпросмотра изображения; коллекция выбирается по Tag
    private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not MediaItemViewModel mediaVm) return;
        if (DataContext is not SearchViewModel vm) return;

        var source = element.Tag switch
        {
            "Person" => vm.PersonImages,
            "Company" => vm.CompanyImages,
            "Event" => vm.EventImages,
            _ => null
        };
        if (source == null || source.Count == 0) return;

        int idx = source.IndexOf(mediaVm);
        if (idx < 0) idx = 0;

        var zoomWindow = new ImageZoomWindow(source.ToList(), idx);
        var owner = Window.GetWindow(this);
        if (owner != null) zoomWindow.Owner = owner;
        zoomWindow.ShowDialog();
    }

    // Открывает окно дубликатов для выбранной персоны
    private void DuplicatesPerson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SearchViewModel vm || vm.SelectedPerson == null) return;
        var services = ((App)Application.Current).Services;
        if (services is null) return;

        var duplicateFinderService = services.GetRequiredService<IDuplicateFinderService>();

        var window = new DuplicateFinderWindow(vm.SelectedPerson.Id, duplicateFinderService)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    // Экспорт выбранной персоны
    private async void ExportPerson_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SearchViewModel vm || vm.SelectedPerson == null) return;
        var title = $"Личность: {vm.SelectedPerson.FullName ?? $"Аноним #{vm.SelectedPerson.Id}"}";
        await ExportAsync(title, MediaOwnerType.Person, vm.SelectedPerson.Id);
    }

    // Экспорт выбранной организации
    private async void ExportCompany_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SearchViewModel vm || vm.SelectedCompany == null) return;
        await ExportAsync($"Организация: {vm.SelectedCompany.Name}", MediaOwnerType.Company, vm.SelectedCompany.Id);
    }

    // Экспорт выбранного события
    private async void ExportEvent_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SearchViewModel vm || vm.SelectedEvent == null) return;
        await ExportAsync($"Событие: {vm.SelectedEvent.Title}", MediaOwnerType.Event, vm.SelectedEvent.Id);
    }

    // Общая логика экспорта: диалог выбора медиа/формата/папки → вызов сервиса
    private async Task ExportAsync(string title, MediaOwnerType ownerType, int ownerId)
    {
        var services = ((App)Application.Current).Services;
        if (services is null) return;

        var exportService = services.GetRequiredService<IExportService>();
        var mediaService = services.GetRequiredService<IMediaService>();

        List<MediaItemViewModel> media;
        try
        {
            media = await mediaService.GetMediaForOwnerAsync(ownerType, ownerId);
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

    // Устанавливает флаг перетаскивания слайдера
    private void Slider_DragStarted(object sender, DragStartedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MediaItemViewModel vm) vm.IsDragging = true;
    }

    // Применяет позицию слайдера после окончания перетаскивания
    private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MediaItemViewModel vm && sender is System.Windows.Controls.Slider slider)
        {
            vm.IsDragging = false;
            vm.Position = slider.Value;
        }
    }
}