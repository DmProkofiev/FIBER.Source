using System;
using System.Collections.Generic;
using System.Windows;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FIBER.Source.Views;

//Диалог экспорта объекта: формат, папка, выбор медиа
public partial class ExportDialogWindow : Window
{
    private readonly ExportDialogViewModel _vm;
    public ExportFormat SelectedFormat { get; private set; }
    public IReadOnlyList<int> SelectedMediaIds { get; private set; } = Array.Empty<int>();
    public string TargetFolder { get; private set; } = string.Empty;
    public ExportDialogWindow(string objectTitle, IEnumerable<MediaItemViewModel> availableMedia)
    {
        InitializeComponent();
        var notificationService = ((App)System.Windows.Application.Current).Services!.GetRequiredService<INotificationService>();
        _vm = new ExportDialogViewModel(objectTitle, availableMedia, OnClose, notificationService);
        DataContext = _vm;
    }

    private void OnClose(bool result)
    {
        if (result)
        {
            SelectedFormat = _vm.SelectedFormat;
            SelectedMediaIds = _vm.GetSelectedMediaIds();
            TargetFolder = _vm.TargetFolder.Trim();
        }
        DialogResult = result;
    }
}