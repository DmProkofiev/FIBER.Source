using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIBER.Source.Services;

// Сервис журнала аудита: пишет в БД и дублирует ошибки в ILogger
public sealed class AuditLogService : IAuditLogService
{
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    private readonly ILogger<AuditLogService> _logger;

    // Внедряет зависимости
    public AuditLogService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        ILogger<AuditLogService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // Пишет запись в журнал; ошибки БД не роняют приложение
    public async Task LogAsync(string action, string details = "")
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            context.AuditLogEntries.Add(new AuditLogEntry
            {
                Action = action,
                Details = details,
                Level = "Info",
                Timestamp = DateTime.UtcNow
            });
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось записать запись аудита {Action}", action);
        }
    }

    // Возвращает последние записи журнала
    public async Task<List<AuditLogEntry>> GetRecentLogsAsync(int count = 100)
    {
        await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        return await context.AuditLogEntries
            .AsNoTracking()
            .OrderByDescending(l => l.Id)
            .Take(count)
            .ToListAsync()
            .ConfigureAwait(false);
    }
}