using FIBER.Source.Helpers;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Infrastructure.Hosting;
using FIBER.Source.Interfaces;
using FIBER.Source.Services;
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

/// <summary>
/// Точка входа приложения: инициализация, аутентификация, DI-хост.
/// </summary>
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

        _databaseService = new DatabaseService();

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

        builder.Services.AddSingleton(_databaseService);

        // VM создаются DI-контейнером вместе со всеми зависимостями (сервисы, DbContextFactory).
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

        builder.Services.AddDbContextFactory<FiberSourceDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IDatabaseService>();
            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = db.DatabasePath,
                Password = db.CurrentPassword,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private
            };
            options.UseSqlite(csb.ToString());
        });

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

        builder.Services.AddSingleton<ITrayService, TrayService>();

        builder.Services.AddHostedService<StorageMaintenanceService>();

        try
        {
            _host = builder.Build();
            await _host.StartAsync();

            using (var scope = _host.Services.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FiberSourceDbContext>>();
                await using var context = await factory.CreateDbContextAsync();
                await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                await context.Database.MigrateAsync();
            }

            var auditLog = _host.Services.GetRequiredService<IAuditLogService>();
            await auditLog.LogAsync("Auth.Startup", $"Version={typeof(App).Assembly.GetName().Version}");

            var mainWindow = new MainWindow
            {
                DataContext = _host.Services.GetRequiredService<MainViewModel>()
            };
            MainWindow = mainWindow;
            mainWindow.Show();

            _trayService = _host.Services.GetRequiredService<ITrayService>();
            _trayService.Initialize(onOpen: RestoreWindow, onExit: ExitApplication);
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
            CleanupAndShutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "FIBER");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
        catch { }

        SqliteConnection.ClearAllPools();
        CleanupHost();
        CleanupMutex();
        base.OnExit(e);
    }

    private void CleanupAndShutdown()
    {
        _isExiting = true;
        _trayService?.Dispose();
        CleanupHost();
        CleanupMutex();
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var audit = _host?.Services.GetService<IAuditLogService>();
            if (audit != null) _ = audit.LogAsync("App.UnhandledException",
                $"{e.Exception.GetType().Name}: {e.Exception.Message}");
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

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        _trayService?.Dispose();
        CleanupHost();
        CleanupMutex();
        Shutdown();
    }

    private void CleanupHost()
    {
        if (_host is null) return;
        try { _host.StopAsync().GetAwaiter().GetResult(); } catch { }
        try { _host.Dispose(); } catch { }
        _host = null;
    }

    private void CleanupMutex()
    {
        if (_mutex is null) return;
        try { _mutex.ReleaseMutex(); } catch { }
        try { _mutex.Dispose(); } catch { }
        _mutex = null;
    }
}