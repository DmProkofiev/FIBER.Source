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
            .FirstOrDefaultAsync(c => c.Id == id);
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
        var company = await context.Companies.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException($"Организация Id={id} не найдена");

        company.Name = input.Name.Trim();
        company.RegistrationNumber = NullIfEmpty(input.RegistrationNumber);
        company.Address = NullIfEmpty(input.Address);
        company.Phone = NullIfEmpty(input.Phone);
        company.Mail = NullIfEmpty(input.Mail);
        company.Website = NullIfEmpty(input.Website);
        company.Notes = NullIfEmpty(input.Notes);

        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Company.Update", $"Id={company.Id}");
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