using System.Windows;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Views;

public partial class IdentificationWindow : Window
{
    public IdentificationWindow(IDatabaseService databaseService)
    {
        InitializeComponent();
        DataContext = new IdentificationWindowViewModel(this, databaseService);
        Loaded += (_, _) => PasswordBox.Focus();
    }
}
