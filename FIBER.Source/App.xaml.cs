using FIBER.Source.Helpers;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Infrastructure.Hosting;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services;
using FIBER.Source.Services.MediaManagement;
using FIBER.Source.Services.MediaSelection;
using FIBER.Source.ViewModels;
using FIBER.Source.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using SQLitePCL;

namespace FIBER.Source;
/// Точка входа приложения: инициализация, аутентификация, DI-хост.
public partial class App : Application
{
    private IHost? _host;
    private ITrayService? _trayService;
    private IDatabaseService? _databaseService;
    private bool _isExiting;
    private static Mutex? _mutex;

    public IServiceProvider? Services => _host?.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try { Batteries_V2.Init(); }
        catch (Exception ex)
        {
            Notification.Show("Ошибка",
                $"Ошибка инициализации SQLite: {ex.Message}",
                NotificationType.Error);
            Shutdown();
            return;
        }

        _mutex = new Mutex(true, "FIBER.Source.App", out bool createdNew);
        if (!createdNew)
        {
            Notification.Show("Внимание",
                "Приложение FIBER Source уже запущено.",
                NotificationType.Warning);
            Shutdown();
            return;
        }

        var databaseAccess = new DatabaseAccessCoordinator();
        var databaseService = new DatabaseService(databaseAccess);
        _databaseService = databaseService;

        var authWindow = new IdentificationWindow(_databaseService);
        if (authWindow.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var authVm = (IdentificationWindowViewModel)authWindow.DataContext;
        if (!authVm.IsAuthenticated)
        {
            Shutdown();
            return;
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IDatabaseService>(databaseService);
        builder.Services.AddSingleton(databaseAccess);
        builder.Services.AddSingleton<MenuViewModel>();
        builder.Services.AddSingleton<CreatePersonViewModel>();
        builder.Services.AddSingleton<CreateCompanyViewModel>();
        builder.Services.AddSingleton<CreateEventViewModel>();
        builder.Services.AddSingleton<SearchViewModel>();
        builder.Services.AddSingleton<UpdatePersonViewModel>();
        builder.Services.AddSingleton<UpdateCompanyViewModel>();
        builder.Services.AddSingleton<UpdateEventViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<IDbContextFactory<FiberSourceDbContext>, RuntimeDbContextFactory>();
        builder.Services.AddSingleton<ISearchStrategy, Services.Search.SearchByIDStrategy>();
        builder.Services.AddSingleton<ISearchStrategy, Services.Search.SearchByFullNameStrategy>();
        builder.Services.AddSingleton<ISearchStrategy, Services.Search.SearchByFullNameBirthdateStrategy>();
        builder.Services.AddSingleton<ISearchStrategy, Services.Search.SearchByPassportStrategy>();
        builder.Services.AddSingleton<ISearchStrategy, Services.Search.SearchByPhoneStrategy>();
        builder.Services.AddSingleton<ISearchStrategy, Services.Search.SearchByAliasStrategy>();
        builder.Services.AddSingleton<ISearchStrategyFactory, Services.Search.SearchStrategyFactory>();
        builder.Services.AddSingleton<IComparisonStrategy, Services.Comparison.ExactMatchStrategy>();
        builder.Services.AddSingleton<IComparisonStrategy, Services.Comparison.LevenshteinStrategy>();
        builder.Services.AddSingleton<IComparisonStrategy, Services.Comparison.MetadataStrategy>();
        builder.Services.AddSingleton<IComparisonStrategyFactory, Services.Comparison.ComparisonStrategyFactory>();
        builder.Services.AddSingleton<IAuditLogService, AuditLogService>();
        builder.Services.AddSingleton<IMediaService, MediaService>();
        builder.Services.AddSingleton<IPersonService, PersonService>();
        builder.Services.AddSingleton<ICountryService, CountryService>();
        builder.Services.AddSingleton<ICompanyService, CompanyService>();
        builder.Services.AddSingleton<IEventService, EventService>();
        builder.Services.AddSingleton<IRelationService, RelationService>();
        builder.Services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();
        builder.Services.AddSingleton<IExportService, ExportService>();
        builder.Services.AddSingleton<ISeedDataService, SeedDataService>();
        builder.Services.AddSingleton<ISettingsDataService, SettingsDataService>();
        builder.Services.AddSingleton<ITrayService, TrayService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<IMediaSelectionDialogService, MediaSelectionDialogService>();
        builder.Services.AddSingleton<IMediaManagementService, MediaManagementService>();
        builder.Services.AddHostedService<StorageMaintenanceService>();

        try
        {
            _host = builder.Build();
            await Task.Run(async () =>
            {
                using var scope = _host.Services.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FiberSourceDbContext>>();
                await using var context = await factory.CreateDbContextAsync();
                await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                await context.Database.MigrateAsync();
                var autoAliases = await context.PersonAliases
                    .Where(a => a.Type == AliasType.LegalName &&
                                context.People.Any(p => p.Id == a.PersonId && p.Status == IdentityStatus.Identified))
                    .ToListAsync();

                if (autoAliases.Count > 0)
                {
                    context.PersonAliases.RemoveRange(autoAliases);
                    await context.SaveChangesAsync();
                }
            });

            await _host.StartAsync();
            var auditLog = _host.Services.GetRequiredService<IAuditLogService>();
            await auditLog.LogAsync("Auth.Startup", $"Version={typeof(App).Assembly.GetName().Version}");
            var mediaService = _host.Services.GetRequiredService<IMediaService>();
            _ = mediaService.PreloadAllMediaAsync();
            var mainWindow = new MainWindow
            {
                DataContext = _host.Services.GetRequiredService<MainViewModel>()
            };
            MainWindow = mainWindow;
            mainWindow.Show();

            _trayService = _host.Services.GetRequiredService<ITrayService>();
            await _trayService.InitializeAsync(onOpen: RestoreWindow, onExit: ExitApplication);
            mainWindow.Closing += MainWindow_Closing;

            var createVm = _host.Services.GetRequiredService<CreatePersonViewModel>();
            await createVm.InitializeAsync();
        }
        catch (Exception ex)
        {
            _isExiting = true;
            Notification.Show("Ошибка запуска",
                $"Критическая ошибка при запуске: {ex.Message}",
                NotificationType.Error);
            await CleanupAndShutdownAsync();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        CleanupMutex();
        base.OnExit(e);
    }

    private async Task CleanupAndShutdownAsync()
    {
        _isExiting = true;
        MainWindow?.Close();
        _trayService?.Dispose();
        _trayService = null;
        var host = _host;
        _host = null;
        if (host is not null) await Task.Run(() => CleanupHostAsync(host));
        await Task.Run(SqliteConnection.ClearAllPools);
        await TemporaryFiles.CleanupAsync(includeCurrentSession: true);
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var audit = _host?.Services.GetService<IAuditLogService>();
            if (audit != null) _ = audit.LogAsync("App.UnhandledException",
                e.Exception.ToString());
        }
        catch { }
        Notification.Show("Ошибка",
            $"Неожиданная ошибка: {e.Exception.Message}",
            NotificationType.Error);
        e.Handled = true;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting) return;
        e.Cancel = true;
        MainWindow?.Hide();
        _trayService?.ShowMinimizeNotification();
    }

    private void RestoreWindow()
    {
        if (MainWindow == null) return;
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    private async void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        await CleanupAndShutdownAsync();
    }

    private static async Task CleanupHostAsync(IHost host)
    {
        try { await host.StopAsync(); } catch { }
        try { host.Dispose(); } catch { }
    }

    private void CleanupMutex()
    {
        if (_mutex is null) return;
        try { _mutex.ReleaseMutex(); } catch { }
        try { _mutex.Dispose(); } catch { }
        _mutex = null;
    }
}
