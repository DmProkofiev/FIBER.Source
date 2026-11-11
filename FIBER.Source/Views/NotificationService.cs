using System;
using System.Windows;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Views;
using Application = System.Windows.Application;

namespace FIBER.Source.Views;

/// <summary>
/// Реализация <see cref="INotificationService"/>, показывающая уведомления через NotificationWindow.
/// Живет в слое Views, чтобы сервисы и ViewModels не зависели от конкретного окна уведомлений.
/// </summary>
public sealed class NotificationService : INotificationService, IDisposable
{
    private bool _disposed;

    public void Show(string title, string message, NotificationType type = NotificationType.Info)
    {
        EnsureDispatcher();
        var window = new NotificationWindow(title, message, type, isConfirm: false);
        AttachOwner(window);
        window.ShowDialog();
    }

    public bool Confirm(string title, string message)
    {
        EnsureDispatcher();
        var window = new NotificationWindow(title, message, NotificationType.Warning, isConfirm: true);
        AttachOwner(window);
        return window.ShowDialog() == true;
    }

    private static void EnsureDispatcher()
    {
        var app = Application.Current;
        if (app is null)
            throw new InvalidOperationException("Application.Current is not available. NotificationService can only be used on the UI thread.");
    }

    private static void AttachOwner(Window window)
    {
        var app = Application.Current;
        if (app is null) return;
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() => AttachOwner(window));
            return;
        }
        var owner = app.MainWindow;
        if (owner is not null && owner.IsVisible && !ReferenceEquals(owner, window))
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
