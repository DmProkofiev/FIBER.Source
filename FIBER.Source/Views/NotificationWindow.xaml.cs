using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FIBER.Source.Helpers;
using Button = System.Windows.Controls.Button;

namespace FIBER.Source.Views;

/// <summary>
/// Модальное окно уведомления или диалога подтверждения в стиле проекта.
/// Использует полностью квалифицированные типы System.Windows.Media/Input,
/// чтобы избежать коллизий с System.Drawing/System.Windows.Forms.
/// </summary>
public partial class NotificationWindow : Window
{
    /// <summary>Создаёт окно с заданным заголовком, сообщением, типом и режимом.</summary>
    public NotificationWindow(string title, string message, NotificationType type, bool isConfirm)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfigureIcon(type);
        BuildButtons(isConfirm);
    }

    /// <summary>Настраивает иконку и цвет по типу уведомления.</summary>
    private void ConfigureIcon(NotificationType type)
    {
        (string glyph, System.Windows.Media.Color color) = type switch
        {
            NotificationType.Success => ("✓", System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60)),
            NotificationType.Warning => ("!", System.Windows.Media.Color.FromRgb(0xF3, 0x9C, 0x12)),
            NotificationType.Error => ("✕", System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C)),
            _ => ("i", System.Windows.Media.Color.FromRgb(0x34, 0x98, 0xDB))
        };
        IconText.Text = glyph;
        IconBorder.Background = new System.Windows.Media.SolidColorBrush(color);
    }

    /// <summary>Создаёт кнопки: OK или Да/Нет.</summary>
    private void BuildButtons(bool isConfirm)
    {
        if (isConfirm)
        {
            var yes = new Button
            {
                Content = "Да",
                Style = (Style)FindResource("PrimaryButtonStyle"),
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0)
            };
            yes.Click += (_, _) => DialogResult = true;

            var no = new Button
            {
                Content = "Нет",
                Width = 100,
                Height = 36,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x7F, 0x7F, 0x7F)),
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Times New Roman"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            no.Click += (_, _) => DialogResult = false;

            ButtonsPanel.Children.Add(yes);
            ButtonsPanel.Children.Add(no);
        }
        else
        {
            var ok = new Button
            {
                Content = "OK",
                Style = (Style)FindResource("PrimaryButtonStyle"),
                Width = 100,
                Height = 36
            };
            ok.Click += (_, _) => DialogResult = true;
            ButtonsPanel.Children.Add(ok);
        }
    }

    /// <summary>Закрывает окно по клику на крестик.</summary>
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>Перетаскивание окна за заголовок.</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}