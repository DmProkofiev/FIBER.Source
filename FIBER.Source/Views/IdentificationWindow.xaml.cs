using System.Windows;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Views;

/// <summary>
/// Окно ввода мастер-пароля для fiber.db.
/// </summary>
public partial class IdentificationWindow : Window
{
    /// <summary>Создаёт окно и VM.</summary>
    public IdentificationWindow(IDatabaseService databaseService)
    {
        InitializeComponent();
        DataContext = new IdentificationWindowViewModel(this, databaseService);
        Loaded += (_, _) => PasswordBox.Focus();
    }

    /// <summary>Проброс ввода пароля из PasswordBox в VM.</summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is IdentificationWindowViewModel vm)
            vm.Password = PasswordBox.Password;
    }
}