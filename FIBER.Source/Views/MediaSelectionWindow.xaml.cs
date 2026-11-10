using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FIBER.Source.ViewModels;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Border = System.Windows.Controls.Border;

namespace FIBER.Source.Views;

/// <summary>Диалог выбора медиафайлов: мультивыбор из общего пула медиа.</summary>
public partial class MediaSelectionWindow : Window
{
    private readonly MediaSelectionViewModel _vm;

    /// <summary>Id выбранных медиа (пусто — ничего не выбрано).</summary>
    public IReadOnlyList<int> SelectedMediaIds { get; private set; } = Array.Empty<int>();

    /// <summary>Создаёт диалог.</summary>
    public MediaSelectionWindow(string title, IEnumerable<MediaItemViewModel> availableMedia, IReadOnlyCollection<int> alreadyAddedMediaIds)
    {
        InitializeComponent();
        _vm = new MediaSelectionViewModel(title, availableMedia, OnClose, alreadyAddedMediaIds);
        DataContext = _vm;
        AttachOwner();
    }

    private void OnClose(bool result)
    {
        if (result)
        {
            SelectedMediaIds = _vm.GetSelectedMediaIds();
        }
        _vm.Dispose();
        DialogResult = result;
    }

    private void AttachOwner()
    {
        var app = Application.Current;
        if (app is null) return;
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(AttachOwner);
            return;
        }
        var owner = app.MainWindow;
        if (owner is not null && owner.IsVisible && !ReferenceEquals(owner, this))
            Owner = owner;
        else
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void PhotoBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not Border border) return;
        if (border.DataContext is not MediaSelectionItem item) return;

        var source = _vm.PhotoItems
            .Where(p => p.IsImage)
            .Select(p => p.Media)
            .ToList();

        if (source.Count == 0)
        {
            source.Add(item.Media);
        }

        int idx = source.IndexOf(item.Media);
        if (idx < 0) idx = 0;

        var zoomWindow = new ImageZoomWindow(source, idx);
        var owner = Window.GetWindow(this);
        if (owner != null) zoomWindow.Owner = owner;
        zoomWindow.ShowDialog();
    }

    private void VideoBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not Border border) return;
        if (border.DataContext is not MediaSelectionItem item) return;

        var source = _vm.VideoItems
            .Where(p => p.IsVideo)
            .Select(p => p.Media)
            .ToList();

        if (source.Count == 0)
        {
            source.Add(item.Media);
        }

        int idx = source.IndexOf(item.Media);
        if (idx < 0) idx = 0;

        var zoomWindow = new VideoZoomWindow(source, idx);
        var owner = Window.GetWindow(this);
        if (owner != null) zoomWindow.Owner = owner;
        zoomWindow.ShowDialog();
    }
}
