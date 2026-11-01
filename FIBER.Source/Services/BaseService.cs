using System;
using System.Threading.Tasks;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

/// <summary>
/// Базовый класс для сервисов, работающих с сущностями и медиа-ссылками.
/// Инкапсулирует общий паттерн удаления: удаление связанных данных + сущности внутри транзакции,
/// затем удаление медиа-ссылок и запись аудита.
/// </summary>
public abstract class BaseService
{
    protected readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    protected readonly IMediaService _mediaService;
    protected readonly IAuditLogService _auditLog;

    protected BaseService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IMediaService mediaService,
        IAuditLogService auditLog)
    {
        _contextFactory = contextFactory;
        _mediaService = mediaService;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Удаляет сущность и её связанные данные внутри транзакции, затем удаляет медиа-ссылки владельца
    /// и записывает событие аудита.
    /// </summary>
    /// <param name="id">Идентификатор удаляемой сущности.</param>
    /// <param name="ownerType">Тип владельца медиа.</param>
    /// <param name="auditAction">Действие для журнала аудита.</param>
    /// <param name="deleteRelatedAndEntityAsync">Логика удаления связанных данных и самой сущности
    /// внутри переданного контекста.</param>
    protected async Task DeleteWithMediaAsync(
        int id,
        MediaOwnerType ownerType,
        string auditAction,
        Func<FiberSourceDbContext, Task> deleteRelatedAndEntityAsync)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            await deleteRelatedAndEntityAsync(context);
            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        await _mediaService.RemoveAllLinksForOwnerAsync(ownerType, id);
        await _auditLog.LogAsync(auditAction, $"Id={id}");
    }
}
