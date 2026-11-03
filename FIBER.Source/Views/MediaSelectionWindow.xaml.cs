using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using FIBER.Source.ViewModels;
using Application = System.Windows.Application;

namespace FIBER.Source.Views;

/// <summary>Диалог выбора медиафайлов: мультивыбор из общего пула медиа.</summary>
public partial class MediaSelectionWindow : Window
{
    private readonly MediaSelectionViewModel _vm;

    /// <summary>Id выбранных медиа (пусто — ничего не выбрано).</summary>
    public IReadOnlyList<int> SelectedMediaIds { get; private set; } = Array.Empty<int>();

    /// <summary>Создаёт диалог.</summary>
    public MediaSelectionWindow(string title, IEnumerable<MediaItemViewModel> availableMedia)
    {
        InitializeComponent();
        _vm = new MediaSelectionViewModel(title, availableMedia, OnClose);
        DataContext = _vm;
        AttachOwner();
    }

    private void OnClose(bool result)
    {
        if (result)
        {
            SelectedMediaIds = _vm.GetSelectedMediaIds();
        }
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
}
