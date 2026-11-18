using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FIBER.Source.Infrastructure.Hosting;

/// <summary>
/// Фоновая служба обслуживания хранилища: проверка целостности БД и очистка временных файлов.
/// </summary>
public sealed class StorageMaintenanceService : BackgroundService
{
    /// <summary>Период очистки временных файлов.</summary>
    private static readonly TimeSpan Period = TimeSpan.FromMinutes(10);

    /// <summary>Логгер.</summary>
    private readonly ILogger<StorageMaintenanceService> _logger;

    /// <summary>Провайдер DI.</summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Инициализирует зависимости.</summary>
    public StorageMaintenanceService(
        ILogger<StorageMaintenanceService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>Запускает проверку при старте и периодическую очистку Temp.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StorageMaintenanceService запущен.");

        try
        {
            await RunFullIntegrityAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки целостности при старте.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(Period, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try { await TemporaryFiles.CleanupAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Ошибка очистки Temp."); }
        }

        _logger.LogInformation("StorageMaintenanceService остановлен.");
    }

    /// <summary>Полная проверка целостности: битые ссылки, осиротевшие медиа.</summary>
    private async Task RunFullIntegrityAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FiberSourceDbContext>>();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        await using var context = await factory.CreateDbContextAsync(ct);
        await context.Database.OpenConnectionAsync(ct);
        foreach (var pragma in new[] { "PRAGMA cipher_integrity_check;", "PRAGMA quick_check;", "PRAGMA foreign_key_check;" })
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = pragma;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var result = reader.GetValue(0)?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"{pragma} {result}");
            }
        }

        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var personIds = await context.People.Select(p => p.Id).ToHashSetAsync(ct);
        var companyIds = await context.Companies.Select(c => c.Id).ToHashSetAsync(ct);
        var eventIds = await context.Events.Select(e => e.Id).ToHashSetAsync(ct);

        var allLinks = await context.MediaAttachmentLinks.ToListAsync(ct);
        var badLinks = allLinks.Where(l => l.OwnerType switch
        {
            MediaOwnerType.Person => !personIds.Contains(l.OwnerId),
            MediaOwnerType.Company => !companyIds.Contains(l.OwnerId),
            MediaOwnerType.Event => !eventIds.Contains(l.OwnerId),
            _ => true
        }).ToList();

        if (badLinks.Count > 0)
        {
            context.MediaAttachmentLinks.RemoveRange(badLinks);
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Удалено битых MediaAttachmentLink: {Count}", badLinks.Count);
        }

        var orphanCount = await context.MediaAttachments
            .Where(m => !m.Links.Any())
            .ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);

        await TemporaryFiles.CleanupAsync();

        if (badLinks.Count + orphanCount > 0)
        {
            await auditLog.LogAsync("Storage.Maintenance",
                $"BadLinks={badLinks.Count}, OrphanMedia={orphanCount}");
        }
    }

}
