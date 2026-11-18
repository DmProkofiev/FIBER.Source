using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace FIBER.Source.ViewModels;

// VM окна идентификации
public partial class IdentificationWindowViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly Window _window;
    public bool IsAuthenticated { get; private set; }
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _loginButtonText = "Войти";
    [ObservableProperty] private bool _isNewDatabase;
    public string DatabaseDisplayPath => _databaseService.DatabasePath;
    public IdentificationWindowViewModel(Window window, IDatabaseService databaseService)
    {
        _window = window;
        _databaseService = databaseService;
        _window.Loaded += async (_, _) =>
        {
            IsNewDatabase = !await _databaseService.DatabaseExistsAsync();
            LoginButtonText = IsNewDatabase ? "Создать базу и войти" : "Войти";
        };
    }
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите пароль";
            return;
        }

        IsBusy = true;
        ErrorMessage = "Проверка пароля";

        try
        {
            if (await _databaseService.DatabaseExistsAsync())
            {
                if (!await _databaseService.TryUnlockAsync(Password))
                {
                    ErrorMessage = "Неверный пароль";
                    return;
                }
            }
            else
            {
                await _databaseService.CreateDatabaseAsync(Password);
            }

            IsAuthenticated = true;
            _window.DialogResult = true;
            _window.Close();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
