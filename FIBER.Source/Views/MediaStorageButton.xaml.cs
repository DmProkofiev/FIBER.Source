using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace FIBER.Source.Views;

/// <summary>
/// Повторно используемая кнопка «Хранилище Медиа».
/// Инкапсулирует Command и оформление — исключает дублирование XAML в Create/Update вью.
/// </summary>
public partial class MediaStorageButton : UserControl
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(MediaStorageButton),
            new PropertyMetadata(null));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public MediaStorageButton()
    {
        InitializeComponent();
    }
}
