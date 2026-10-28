using System.Windows;
using FIBER.Source.Views;
using Application = System.Windows.Application;

namespace FIBER.Source.Helpers;

public enum NotificationType
{
    /// <summary>Информационное сообщение</summary>
    Info,
    /// <summary>Успешная операция</summary>
    Success,
    /// <summary>Предупреждение</summary>
    Warning,
    /// <summary>Ошибка</summary>
    Error
}

/// <summary>
/// Статический помощник для показа кастомных уведомлений и диалогов подтверждения
/// </summary>
public static class Notification
{
    /// <summary>Показывает модальное уведомление с кнопкой OK</summary>
    public static void Show(string title, string message, NotificationType type = NotificationType.Info)
    {
        var window = new NotificationWindow(title, message, type, isConfirm: false);
        AttachOwner(window);
        window.ShowDialog();
    }

    /// <summary>Показывает модальный диалог подтверждения с кнопками Да/Нет</summary>
    public static bool Confirm(string title, string message)
    {
        var window = new NotificationWindow(title, message, NotificationType.Warning, isConfirm: true);
        AttachOwner(window);
        return window.ShowDialog() == true;
    }

    /// <summary>Привязывает владельца окна или ставит его в центр экрана</summary>
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
}