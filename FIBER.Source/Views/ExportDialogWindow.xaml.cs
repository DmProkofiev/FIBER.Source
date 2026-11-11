using System;
using System.Collections.Generic;
using System.Windows;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FIBER.Source.Views;

/// <summary>Диалог экспорта объекта: формат, папка, выбор медиа.</summary>
public partial class ExportDialogWindow : Window
{
    private readonly ExportDialogViewModel _vm;

    /// <summary>Выбранный формат.</summary>
    public ExportFormat SelectedFormat { get; private set; }

    /// <summary>Id выбранных медиа (пусто — без медиа, null не бывает).</summary>
    public IReadOnlyList<int> SelectedMediaIds { get; private set; } = Array.Empty<int>();

    /// <summary>Папка назначения.</summary>
    public string TargetFolder { get; private set; } = string.Empty;

    /// <summary>Создаёт диалог.</summary>
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