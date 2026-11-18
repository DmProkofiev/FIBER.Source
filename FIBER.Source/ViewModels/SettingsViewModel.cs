using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FIBER.Source.ViewModels;

// Настройки: списки сущностей, медиатека, журнал, смена пароля, экспорт БД
public partial class SettingsViewModel : ObservableObject
{
    // Сервис аудита
    private readonly IAuditLogService _auditLogService;
    private readonly ISettingsDataService _settingsDataService;
    private int _loadVersion;
    // Сервис медиа
    private readonly IMediaService _mediaService;
    // Сервис организаций
    private readonly ICompanyService _companyService;
    // Сервис событий
    private readonly IEventService _eventService;
    // Сервис управления БД
    private readonly IDatabaseService _databaseService;
    // Сервис сидинга тестовых данных
    private readonly ISeedDataService _seedDataService;
    // Сервис уведомлений
    private readonly INotificationService _notificationService;
    // Запрос на редактирование организации
    public event Action<int>? UpdateCompanyRequested;
    // Запрос на редактирование события 
    public event Action<int>? UpdateEventRequested;
    // Инициализирует зависимости 
    public SettingsViewModel(
        IAuditLogService auditLogService,
        ISettingsDataService settingsDataService,
        IMediaService mediaService,
        ICompanyService companyService,
        IEventService eventService,
        IDatabaseService databaseService,
        ISeedDataService seedDataService,
        INotificationService notificationService)
    {
        _auditLogService = auditLogService;
        _settingsDataService = settingsDataService;
        _mediaService = mediaService;
        _companyService = companyService;
        _eventService = eventService;
        _databaseService = databaseService;
        _seedDataService = seedDataService;
        _notificationService = notificationService;
    }

    //Люди
    public ObservableCollection<PersonSummary> People { get; } = new();
    //Организации
    public ObservableCollection<CompanySummary> Companies { get; } = new();
    //События
    public ObservableCollection<EventSummary> Events { get; } = new();
    //Журнал событий
    public ObservableCollection<AuditLogEntry> Logs { get; } = new();
    //Медиатека
    public ObservableCollection<SettingsMediaItemViewModel> AllMedia { get; } = new();
    //Всего медиафайлов
    [ObservableProperty] private int _mediaTotal;
    
    [ObservableProperty] private bool _isBusy;
    //Версия приложения
    [ObservableProperty] private string _appVersion = "FIBER Source 1.0.0";
    //Путь к файлу БД
    [ObservableProperty] private string _dbPath = string.Empty;
    //Результат последнего сидинга
    [ObservableProperty] private string _seedResult = string.Empty;
    //Старый пароль
    [ObservableProperty] private string _oldPassword = string.Empty;
    // Новый пароль
    [ObservableProperty] private string _newPassword = string.Empty;
    //Подтверждение
    [ObservableProperty] private string _confirmPassword = string.Empty;
    //Загружает данные
    public async Task LoadDataAsync()
    {
        var version = ++_loadVersion;
        IsBusy = true;
        try
        {
            DbPath = _databaseService.DatabasePath;
            var data = await _settingsDataService.LoadAsync();
            if (version != _loadVersion) return;
            Replace(People, data.People);
            Replace(Companies, data.Companies);
            Replace(Events, data.Events);
            Replace(Logs, data.Logs);
            foreach (var old in AllMedia) old.Dispose();
            AllMedia.Clear();
            foreach (var item in data.Media)
            {
                var media = item.Media.ToViewModel(_mediaService);
                AllMedia.Add(new SettingsMediaItemViewModel(media, item.OwnerType, item.OwnerId, item.OwnerName));
            }
            MediaTotal = AllMedia.Count;
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) _notificationService.Show("Ошибка загрузки настроек", ex.Message, NotificationType.Error);
        }
        finally { if (version == _loadVersion) IsBusy = false; }
    }

    //Заменяет содержимое коллекции
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var x in source) target.Add(x);
    }

    //Смена пароля
    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            _notificationService.Show("Внимание", "Заполните все поля паролей", NotificationType.Warning);
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            _notificationService.Show("Ошибка", "Новый пароль и подтверждение не совпадают", NotificationType.Error);
            return;
        }
        try
        {
            await _databaseService.ChangePasswordAsync(OldPassword, NewPassword);
            await _auditLogService.LogAsync("Auth.ChangePassword", string.Empty);
            _notificationService.Show("Успех", "Пароль успешно изменён!", NotificationType.Success);
            OldPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("пароль"))
        {
            _notificationService.Show("Ошибка", "Неверный текущий пароль.", NotificationType.Error);
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка смены пароля", ex.Message, NotificationType.Error);
        }
    }
    //Экспорт зашифрованной копии
    [RelayCommand]
    private async Task ExportEncryptedAsync() => await ExportAsync(encrypted: true);
    //Экспорт в открытом виде
    [RelayCommand]
    private async Task ExportUnencryptedAsync() => await ExportAsync(encrypted: false);
    //>Общая логика экспорта БД
    private async Task ExportAsync(bool encrypted)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "SQLite database (*.db)|*.db|Все файлы (*.*)|*.*",
            FileName = encrypted ? "fiber_backup.db" : "fiber_export_plain.db",
            Title = encrypted ? "Экспорт зашифрованной копии" : "Экспорт БД в открытом виде"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _databaseService.ExportAsync(dialog.FileName, encrypted);
            await _auditLogService.LogAsync("Database.Export", $"Path={dialog.FileName}, Encrypted={encrypted}");
            _notificationService.Show("Экспорт завершён",
                encrypted
                    ? $"Зашифрованная копия сохранена:\n{dialog.FileName}"
                    : $"БД экспортирована в открытом виде:\n{dialog.FileName}",
                NotificationType.Success);
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка экспорта", ex.Message, NotificationType.Error);
        }
    }

    //аполнить БД тестовыми данными
    [RelayCommand]
    private async Task SeedDataAsync()
    {
        try
        {
            var result = await _seedDataService.SeedAsync();
            if (result.Skipped)
            {
                SeedResult = "В БД уже есть данные — сидинг пропущен.";
                _notificationService.Show("Информация", SeedResult, NotificationType.Warning);
            }
            else
            {
                SeedResult =
                    $"Создано: персон {result.PersonsCreated}, организаций {result.CompaniesCreated}, " +
                    $"событий {result.EventsCreated}, связей {result.RelationshipsCreated}.";
                await _auditLogService.LogAsync("Database.Seed", SeedResult);
                _notificationService.Show("Сидинг завершён", SeedResult, NotificationType.Success);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка сидинга", ex.Message, NotificationType.Error);
        }
    }

    //Открыть редактирование организации
    [RelayCommand]
    private void EditCompany(CompanySummary summary) { if (summary != null) UpdateCompanyRequested?.Invoke(summary.Id); }

    //>Удалить организацию
    [RelayCommand]
    private async Task DeleteCompanyAsync(CompanySummary summary)
    {
        if (summary == null) return;
        if (!_notificationService.Confirm("Удаление организации", $"Удалить организацию?\n\n{summary.Name}")) return;
        try
        {
            await _companyService.DeleteAsync(summary.Id);
            await LoadDataAsync();
        }
        catch (Exception ex) { _notificationService.Show("Ошибка", ex.Message, NotificationType.Error); }
    }

    //Открыть редактирование события
    [RelayCommand]
    private void EditEvent(EventSummary summary) { if (summary != null) UpdateEventRequested?.Invoke(summary.Id); }

    //Удалить событие
    [RelayCommand]
    private async Task DeleteEventAsync(EventSummary summary)
    {
        if (summary == null) return;
        if (!_notificationService.Confirm("Удаление события", $"Удалить событие?\n\n{summary.Title}")) return;
        try
        {
            await _eventService.DeleteAsync(summary.Id);
            await LoadDataAsync();
        }
        catch (Exception ex) { _notificationService.Show("Ошибка", ex.Message, NotificationType.Error); }
    }
}
