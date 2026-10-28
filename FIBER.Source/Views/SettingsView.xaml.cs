using System.Windows;
using FIBER.Source.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace FIBER.Source.Views;

// Code-behind настроек: проброс пароля из PasswordBox в VM
public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    // Старый пароль
    private void OldPassBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.OldPassword = OldPassBox.Password;
    }

    // Новый пароль
    private void NewPassBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.NewPassword = NewPassBox.Password;
    }

    // Подтверждение
    private void ConfirmPassBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.ConfirmPassword = ConfirmPassBox.Password;
    }
}