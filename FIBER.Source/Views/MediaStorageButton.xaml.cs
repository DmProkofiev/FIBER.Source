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

    public static readonly DependencyProperty FileNameProperty =
        DependencyProperty.Register(
            nameof(FileName),
            typeof(string),
            typeof(MediaStorageButton),
            new PropertyMetadata(string.Empty));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public string? FileName
    {
        get => (string?)GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    public MediaStorageButton()
    {
        InitializeComponent();
    }
}
