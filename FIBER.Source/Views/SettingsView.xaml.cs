using System.Windows;
using FIBER.Source.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace FIBER.Source.Views;

// Code-behind настроек
public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private async void MediaItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SettingsMediaItemViewModel item } && item.Media.IsImage)
            await item.Media.LoadImageAsync();
    }
}
