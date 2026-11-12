using FIBER.Source.Interfaces;
using System;
using System.IO;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace FIBER.Source.Services;

public class TrayService : ITrayService
{
    private Forms.NotifyIcon? _notifyIcon;
    private Drawing.Icon? _icon;
    private bool _isBalloonShown;
    private bool _isDisposed;

    public void Initialize(Action onOpen, Action onExit)
    {
        // Защита от дублирования: если иконка уже создана или сервис disposal-лен — не создаём повторно.
        if (_notifyIcon != null || _isDisposed) return;

        _notifyIcon = new Forms.NotifyIcon();

        // Загружаем иконку из ресурсов
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "FIBERICO.ico");
        if (File.Exists(iconPath))
        {
            _icon = new Drawing.Icon(iconPath);
            _notifyIcon.Icon = _icon;
        }
        else
        {
            _notifyIcon.Icon = Drawing.SystemIcons.Application;
        }

        _notifyIcon.Text = "FIBER Source";

        // Создаем контекстное меню
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Развернуть", null, (_, _) => onOpen());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => onExit());

        _notifyIcon.ContextMenuStrip = menu;

        // Обработчики клика
        _notifyIcon.DoubleClick += (_, _) => onOpen();
        _notifyIcon.Visible = true;
    }

    public void ShowMinimizeNotification()
    {
        if (_notifyIcon == null || _isBalloonShown) return;

        _notifyIcon.ShowBalloonTip(3000,
            "FIBER Source",
            "Приложение свернуто в системный трей и продолжает работать в фоне.",
            Forms.ToolTipIcon.Info);

        _isBalloonShown = true;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        if (_icon != null)
        {
            _icon.Dispose();
            _icon = null;
        }
    }
}