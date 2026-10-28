using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public sealed class CompanyService : ICompanyService
{
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    private readonly IMediaService _mediaService;
    private readonly IAuditLogService _auditLog;

    public CompanyService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IMediaService mediaService,
        IAuditLogService auditLog)
    {
        _contextFactory = contextFactory;
        _mediaService = mediaService;
        _auditLog = auditLog;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<Company> CreateAsync(CompanyInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var company = new Company
        {
            Name = input.Name.Trim(),
            RegistrationNumber = NullIfEmpty(input.RegistrationNumber),
            Address = NullIfEmpty(input.Address),
            Phone = NullIfEmpty(input.Phone),
            Mail = NullIfEmpty(input.Mail),
            Website = NullIfEmpty(input.Website),
            Notes = NullIfEmpty(input.Notes)
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Company.Create", $"Id={company.Id}");
        return company;
    }

    public async Task<Company?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Companies.AsNoTracking()
            // Взаимодействия компании: прямые и обратные (чтобы в редакторе были видны обе стороны).
            .Include(c => c.OutgoingInteractions).ThenInclude(ci => ci.TargetCompany)
            .Include(c => c.IncomingInteractions).ThenInclude(ci => ci.SourceCompany)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IReadOnlyList<Company>> GetAllExceptAsync(int excludeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Companies.AsNoTracking()
            .Where(c => c.Id != excludeId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CompanySummary>> ListAsync(string? query)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        IQueryable<Company> q = context.Companies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var t = query.Trim();
            q = q.Where(c => c.Name.Contains(t));
        }
        return await q.OrderBy(c => c.Name)
            .Select(c => new CompanySummary(c.Id, c.Name, c.RegistrationNumber))
            .ToListAsync();
    }

    public async Task UpdateAsync(int id, CompanyInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var company = await context.Companies
            .Include(c => c.OutgoingInteractions)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Организация Id={id} не найдена");

        company.Name = input.Name.Trim();
        company.RegistrationNumber = NullIfEmpty(input.RegistrationNumber);
        company.Address = NullIfEmpty(input.Address);
        company.Phone = NullIfEmpty(input.Phone);
        company.Mail = NullIfEmpty(input.Mail);
        company.Website = NullIfEmpty(input.Website);
        company.Notes = NullIfEmpty(input.Notes);

        // Взаимодействия с другими компаниями: добавление / изменение / удаление.
        await SyncInteractionsAsync(context, company, input.Interactions);

        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Company.Update", $"Id={company.Id}");
    }

    // ===== Синхронизация связей «компания → компания» (CRUD внутри Update) =====

    // Полная синхронизация исходящих взаимодействий компании: новые добавляются,
    // изменённые обновляются, отсутствующие удаляются.
    // Дубликаты (одна и та же пара компаний) запрещены — как в RelationService.AddCompanyInteractionAsync.
    private static async Task SyncInteractionsAsync(
        FiberSourceDbContext context, Company company, IReadOnlyList<CompanyInteractionEditInput>? inputs)
    {
        if (inputs is null) return; // null — не трогать связи; пустой список — удалить все.

        // DELETE: исходящие связи, отсутствующие во входных данных.
        foreach (var existing in company.OutgoingInteractions.ToList())
        {
            var keep = inputs.Any(i => i.TargetCompanyId == existing.TargetCompanyId);
            if (!keep)
            {
                company.OutgoingInteractions.Remove(existing);
                context.CompanyInteractions.Remove(existing);
            }
        }

        // UPDATE + CREATE.
        foreach (var input in inputs.Where(i => i.TargetCompanyId.HasValue))
        {
            var targetId = input.TargetCompanyId!.Value;
            if (targetId == company.Id)
                throw new InvalidOperationException("Связь организации сама с собой невозможна");

            if (!await context.Companies.AnyAsync(c => c.Id == targetId))
                throw new InvalidOperationException($"Организация Id={targetId} не найдена");

            var type = (input.InteractionType ?? string.Empty).Trim();
            var notes = NullIfEmpty(input.Notes) ?? string.Empty;

            var link = company.OutgoingInteractions.FirstOrDefault(ci => ci.TargetCompanyId == targetId);
            if (link is not null)
            {
                // Существующая связь — обновляем тип/заметки.
                link.InteractionType = type;
                link.Notes = notes;
                continue;
            }

            // Защита от дубля: обратная связь уже существует (target → source).
            var reverseExists = await context.CompanyInteractions.AnyAsync(ci =>
                ci.SourceCompanyId == targetId && ci.TargetCompanyId == company.Id);
            if (reverseExists)
                throw new InvalidOperationException(
                    "Взаимодействие между этими организациями уже существует (обратная связь)");

            // Защита от дубля внутри самого набора входа.
            if (inputs.Count(i => i.TargetCompanyId == targetId) > 1)
                throw new InvalidOperationException(
                    $"Организация Id={targetId} указана во взаимодействиях дважды");

            context.CompanyInteractions.Add(new CompanyInteraction
            {
                SourceCompanyId = company.Id,
                TargetCompanyId = targetId,
                InteractionType = type,
                Notes = notes
            });
        }
    }

    public async Task DeleteAsync(int id)
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        await using (var tx = await context.Database.BeginTransactionAsync())
        {
            try
            {
                var interactions = await context.CompanyInteractions
                    .Where(ci => ci.SourceCompanyId == id || ci.TargetCompanyId == id)
                    .ToListAsync();
                context.CompanyInteractions.RemoveRange(interactions);

                var company = await context.Companies.FirstOrDefaultAsync(c => c.Id == id);
                if (company is not null) context.Companies.Remove(company);

                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        await _mediaService.RemoveAllLinksForOwnerAsync(MediaOwnerType.Company, id);
        await _auditLog.LogAsync("Company.Delete", $"Id={id}");
    }
}