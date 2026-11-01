using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;
using FIBER.Source.Services.Search;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public sealed class CompanyService : BaseService, ICompanyService
{
    public CompanyService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IMediaService mediaService,
        IAuditLogService auditLog)
        : base(contextFactory, mediaService, auditLog)
    {
    }

    public async Task<Company> CreateAsync(CompanyInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var company = new Company
        {
            Name = input.Name.Trim(),
            RegistrationNumber = StringHelpers.NullIfEmpty(input.RegistrationNumber),
            Address = StringHelpers.NullIfEmpty(input.Address),
            Phone = StringHelpers.NullIfEmpty(input.Phone),
            Mail = StringHelpers.NullIfEmpty(input.Mail),
            Website = StringHelpers.NullIfEmpty(input.Website),
            Notes = StringHelpers.NullIfEmpty(input.Notes)
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
            // SQLite NOCASE не работает для кириллицы, поэтому перебираем варианты регистра
            // через EF.Functions.Like (транслируется в SQL).
            var term = SearchTerm.NormalizeWord(query);
            var predicate = PredicateBuilder.False<Company>();
            foreach (var variant in SqliteLike.CaseVariants(term))
            {
                var escaped = SqliteLike.Escape(variant);
                predicate = predicate.Or(c => EF.Functions.Like(c.Name, "%" + escaped + "%"));
            }
            q = q.Where(predicate);
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
        company.RegistrationNumber = StringHelpers.NullIfEmpty(input.RegistrationNumber);
        company.Address = StringHelpers.NullIfEmpty(input.Address);
        company.Phone = StringHelpers.NullIfEmpty(input.Phone);
        company.Mail = StringHelpers.NullIfEmpty(input.Mail);
        company.Website = StringHelpers.NullIfEmpty(input.Website);
        company.Notes = StringHelpers.NullIfEmpty(input.Notes);

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
            var notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty;

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
        await DeleteWithMediaAsync(
            id,
            MediaOwnerType.Company,
            "Company.Delete",
            async context =>
            {
                var interactions = await context.CompanyInteractions
                    .Where(ci => ci.SourceCompanyId == id || ci.TargetCompanyId == id)
                    .ToListAsync();
                context.CompanyInteractions.RemoveRange(interactions);

                var company = await context.Companies.FirstOrDefaultAsync(c => c.Id == id);
                if (company is not null) context.Companies.Remove(company);
            });
    }
}