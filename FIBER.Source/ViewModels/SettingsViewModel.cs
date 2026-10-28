using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FIBER.Source.ViewModels;

/// <summary>Настройки: списки сущностей, медиатека, журнал, смена пароля, экспорт БД.</summary>
public partial class SettingsViewModel : ObservableObject
{
    /// <summary>Сервис аудита.</summary>
    private readonly IAuditLogService _auditLogService;
    /// <summary>Фабрика контекста.</summary>
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    /// <summary>Сервис медиа.</summary>
    private readonly IMediaService _mediaService;
    /// <summary>Сервис организаций.</summary>
    private readonly ICompanyService _companyService;
    /// <summary>Сервис событий.</summary>
    private readonly IEventService _eventService;
    /// <summary>Сервис управления БД.</summary>
    private readonly IDatabaseService _databaseService;

    /// <summary>Запрос на редактирование организации.</summary>
    public event Action<int>? UpdateCompanyRequested;
    /// <summary>Запрос на редактирование события.</summary>
    public event Action<int>? UpdateEventRequested;

    /// <summary>Инициализирует зависимости.</summary>
    public SettingsViewModel(
        IAuditLogService auditLogService,
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IMediaService mediaService,
        ICompanyService companyService,
        IEventService eventService,
        IDatabaseService databaseService)
    {
        _auditLogService = auditLogService;
        _contextFactory = contextFactory;
        _mediaService = mediaService;
        _companyService = companyService;
        _eventService = eventService;
        _databaseService = databaseService;
    }

    /// <summary>Люди.</summary>
    public ObservableCollection<PersonSummary> People { get; } = new();
    /// <summary>Организации.</summary>
    public ObservableCollection<CompanySummary> Companies { get; } = new();
    /// <summary>События.</summary>
    public ObservableCollection<EventSummary> Events { get; } = new();
    /// <summary>Журнал событий.</summary>
    public ObservableCollection<AuditLogEntry> Logs { get; } = new();
    /// <summary>Медиатека.</summary>
    public ObservableCollection<SettingsMediaItemViewModel> AllMedia { get; } = new();

    /// <summary>Всего медиафайлов.</summary>
    [ObservableProperty] private int _mediaTotal;
    /// <summary>Версия приложения.</summary>
    [ObservableProperty] private string _appVersion = "FIBER Source 1.0.0";
    /// <summary>Путь к файлу БД.</summary>
    [ObservableProperty] private string _dbPath = string.Empty;
    /// <summary>Старый пароль.</summary>
    [ObservableProperty] private string _oldPassword = string.Empty;
    /// <summary>Новый пароль.</summary>
    [ObservableProperty] private string _newPassword = string.Empty;
    /// <summary>Подтверждение.</summary>
    [ObservableProperty] private string _confirmPassword = string.Empty;

    /// <summary>Загружает данные.</summary>
    public async Task LoadDataAsync()
    {
        DbPath = _databaseService.DatabasePath;

        await using var context = await _contextFactory.CreateDbContextAsync();

        var mediaCounts = await context.MediaAttachmentLinks
            .GroupBy(l => new { l.OwnerType, l.OwnerId })
            .Select(g => new { g.Key.OwnerType, g.Key.OwnerId, Count = g.Count() })
            .ToListAsync();
        var mediaByOwner = mediaCounts.ToDictionary(x => (x.OwnerType, x.OwnerId), x => x.Count);

        var peopleRaw = await context.People.AsNoTracking()
            .Where(p => p.MergedIntoPersonId == null)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.FullName, p.Status, p.BirthDate })
            .ToListAsync();
        Replace(People, peopleRaw.Select(p => new PersonSummary(
            p.Id, p.FullName ?? "Аноним #" + p.Id, p.Status, p.BirthDate,
            mediaByOwner.GetValueOrDefault((MediaOwnerType.Person, p.Id), 0))));

        var companiesRaw = await _companyService.ListAsync(null);
        Replace(Companies, companiesRaw.Select(c => c with
        { MediaCount = mediaByOwner.GetValueOrDefault((MediaOwnerType.Company, c.Id), 0) }));

        var eventsRaw = await _eventService.ListAsync();
        Replace(Events, eventsRaw.Select(e => e with
        { MediaCount = mediaByOwner.GetValueOrDefault((MediaOwnerType.Event, e.Id), 0) }));

        Replace(Logs, await _auditLogService.GetRecentLogsAsync(100));

        foreach (var item in AllMedia) item.Dispose();
        AllMedia.Clear();

        var allMedia = await _mediaService.GetAllMediaAsync();

        var personIds = allMedia.Where(m => m.OwnerType == MediaOwnerType.Person).Select(m => m.OwnerId).Distinct().ToList();
        var companyIds = allMedia.Where(m => m.OwnerType == MediaOwnerType.Company).Select(m => m.OwnerId).Distinct().ToList();
        var eventIds = allMedia.Where(m => m.OwnerType == MediaOwnerType.Event).Select(m => m.OwnerId).Distinct().ToList();

        var peopleDict = await context.People.Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FullName ?? $"Аноним #{p.Id}");
        var companiesDict = await context.Companies.Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var eventsDict = await context.Events.Where(e => eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Title);

        foreach (var item in allMedia)
        {
            var ownerName = item.OwnerType switch
            {
                MediaOwnerType.Person => peopleDict.GetValueOrDefault(item.OwnerId, "Удалён"),
                MediaOwnerType.Company => companiesDict.GetValueOrDefault(item.OwnerId, "Удалён"),
                MediaOwnerType.Event => eventsDict.GetValueOrDefault(item.OwnerId, "Удалён"),
                _ => "Неизвестно"
            };
            AllMedia.Add(new SettingsMediaItemViewModel(item.Media, item.OwnerType, item.OwnerId, ownerName));
        }
        MediaTotal = AllMedia.Count;
    }

    /// <summary>Заменяет содержимое коллекции.</summary>
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var x in source) target.Add(x);
    }

    /// <summary>Смена мастер-пароля.</summary>
    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            Notification.Show("Внимание", "Заполните все поля паролей.", NotificationType.Warning);
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            Notification.Show("Ошибка", "Новый пароль и подтверждение не совпадают.", NotificationType.Error);
            return;
        }
        try
        {
            await _databaseService.ChangePasswordAsync(OldPassword, NewPassword);
            await _auditLogService.LogAsync("Auth.ChangePassword", string.Empty);
            Notification.Show("Успех", "Пароль успешно изменён!", NotificationType.Success);
            OldPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("пароль"))
        {
            Notification.Show("Ошибка", "Неверный текущий пароль.", NotificationType.Error);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка смены пароля", ex.Message, NotificationType.Error);
        }
    }

    /// <summary>Экспорт зашифрованной копии.</summary>
    [RelayCommand]
    private async Task ExportEncryptedAsync() => await ExportAsync(encrypted: true);

    /// <summary>Экспорт в открытом виде.</summary>
    [RelayCommand]
    private async Task ExportUnencryptedAsync() => await ExportAsync(encrypted: false);

    /// <summary>Общая логика экспорта БД.</summary>
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
            Notification.Show("Экспорт завершён",
                encrypted
                    ? $"Зашифрованная копия сохранена:\n{dialog.FileName}"
                    : $"БД экспортирована в открытом виде:\n{dialog.FileName}",
                NotificationType.Success);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка экспорта", ex.Message, NotificationType.Error);
        }
    }

    /// <summary>Открыть редактирование организации.</summary>
    [RelayCommand]
    private void EditCompany(CompanySummary summary) { if (summary != null) UpdateCompanyRequested?.Invoke(summary.Id); }

    /// <summary>Удалить организацию.</summary>
    [RelayCommand]
    private async Task DeleteCompanyAsync(CompanySummary summary)
    {
        if (summary == null) return;
        if (!Notification.Confirm("Удаление организации", $"Удалить организацию?\n\n{summary.Name}")) return;
        try
        {
            await _companyService.DeleteAsync(summary.Id);
            await LoadDataAsync();
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
    }

    /// <summary>Открыть редактирование события.</summary>
    [RelayCommand]
    private void EditEvent(EventSummary summary) { if (summary != null) UpdateEventRequested?.Invoke(summary.Id); }

    /// <summary>Удалить событие.</summary>
    [RelayCommand]
    private async Task DeleteEventAsync(EventSummary summary)
    {
        if (summary == null) return;
        if (!Notification.Confirm("Удаление события", $"Удалить событие?\n\n{summary.Title}")) return;
        try
        {
            await _eventService.DeleteAsync(summary.Id);
            await LoadDataAsync();
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
    }
}