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

    public async Task InitializeAsync(Action onOpen, Action onExit)
    {
        // Защита от дублирования: если иконка уже создана или сервис disposal-лен — не создаём повторно.
        if (_notifyIcon != null || _isDisposed) return;

        // Загружаем иконку из ресурсов
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "FIBERICO.ico");
        Drawing.Icon? icon = null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(iconPath);
            using var stream = new MemoryStream(bytes);
            icon = new Drawing.Icon(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("Не удалось загрузить иконку трея: {0}", ex);
        }
        if (_isDisposed) { icon?.Dispose(); return; }
        _icon = icon;
        _notifyIcon = new Forms.NotifyIcon { Icon = icon ?? Drawing.SystemIcons.Application };

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
            var menu = _notifyIcon.ContextMenuStrip;
            _notifyIcon.ContextMenuStrip = null;
            _notifyIcon.Dispose();
            menu?.Dispose();
            _notifyIcon = null;
        }

        if (_icon != null)
        {
            _icon.Dispose();
            _icon = null;
        }
    }
}
