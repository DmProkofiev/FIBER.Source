using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace FIBER.Source.ViewModels;

/// <summary>
/// VM окна идентификации: ввод мастер-пароля для fiber.db.
/// </summary>
public partial class IdentificationWindowViewModel : ObservableObject
{
    /// <summary>Сервис управления БД.</summary>
    private readonly IDatabaseService _databaseService;

    /// <summary>Ссылка на окно.</summary>
    private readonly Window _window;

    /// <summary>Признак успешной аутентификации.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>Введённый мастер-пароль.</summary>
    [ObservableProperty] private string _password = string.Empty;

    /// <summary>Сообщение об ошибке.</summary>
    [ObservableProperty] private string _errorMessage = string.Empty;

    /// <summary>Признак занятости.</summary>
    [ObservableProperty] private bool _isBusy;

    /// <summary>Текст кнопки входа (зависит от существования БД).</summary>
    [ObservableProperty] private string _loginButtonText = "Войти";

    /// <summary>Признак необходимости создания новой БД.</summary>
    [ObservableProperty] private bool _isNewDatabase;

    /// <summary>Отображаемый путь к файлу БД.</summary>
    public string DatabaseDisplayPath => _databaseService.DatabasePath;

    /// <summary>Инициализирует зависимости и проверяет наличие БД.</summary>
    public IdentificationWindowViewModel(Window window, IDatabaseService databaseService)
    {
        _window = window;
        _databaseService = databaseService;
        IsNewDatabase = !_databaseService.DatabaseExists();
        LoginButtonText = IsNewDatabase ? "Создать базу и войти" : "Войти";
    }

    /// <summary>Выполняет вход или создание БД.</summary>
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите пароль";
            return;
        }

        IsBusy = true;
        ErrorMessage = "Подготовка...";

        try
        {
            if (_databaseService.DatabaseExists())
            {
                if (!await _databaseService.TryUnlockAsync(Password))
                {
                    ErrorMessage = "Неверный мастер-пароль";
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