using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FIBER.Source.ViewModels;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;

namespace FIBER.Source.Views;

public partial class UpdatePersonView : UserControl
{
    public UpdatePersonView()
    {
        InitializeComponent();
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        // Опционально: визуальная реакция на уход курсора
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (DataContext is UpdatePersonViewModel vm)
            {
                await vm.HandleFilesDropAsync(files);
            }
        }
    }

    /// <summary>Находит MediaElement в том же визуальном контейнере, что и кнопка.</summary>
    private static MediaElement? FindMediaElement(Button button)
    {
        var current = (DependencyObject)button;
        while (current is not null)
        {
            var count = VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is MediaElement me)
                    return me;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void MediaPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var media = FindMediaElement(btn);
            media?.Play();
        }
    }

    private void MediaPause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var media = FindMediaElement(btn);
            media?.Pause();
        }
    }

    private void MediaStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var media = FindMediaElement(btn);
            media?.Stop();
        }
    }
}