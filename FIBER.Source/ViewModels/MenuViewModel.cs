using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace FIBER.Source.ViewModels;

// VM главного меню: раскрытие подменю «Создать объект» и навигация
public partial class MenuViewModel : ObservableObject
{
    public event Action<string>? NavigationRequested;

    // Подменю типов создаваемых объектов открыто/закрыто
    [ObservableProperty]
    private bool _isCreateSubmenuOpen;

    // Переключает видимость подменю «Создать объект»
    [RelayCommand]
    private void ToggleCreateSubmenu() => IsCreateSubmenuOpen = !IsCreateSubmenuOpen;

    // Выполняет навигацию и закрывает подменю
    [RelayCommand]
    private void Navigate(string key)
    {
        IsCreateSubmenuOpen = false;
        NavigationRequested?.Invoke(key);
    }

    // Принудительно закрывает подменю (используется при возврате в меню)
    public void CloseSubmenu() => IsCreateSubmenuOpen = false;
}