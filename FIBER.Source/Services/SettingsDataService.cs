using FIBER.Source.Contracts;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.Search;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public sealed class SettingsDataService(
    IDbContextFactory<FiberSourceDbContext> factory,
    ICompanyService companyService, IEventService eventService,
    IMediaService mediaService, IAuditLogService auditLog) : ISettingsDataService
{
    public async Task<SettingsData> LoadAsync()
    {
        await BackgroundWork.SwitchToThreadPool();
        await using var context = await factory.CreateDbContextAsync();
        var counts = await context.MediaAttachmentLinks
            .GroupBy(l => new { l.OwnerType, l.OwnerId })
            .Select(g => new { g.Key.OwnerType, g.Key.OwnerId, Count = g.Count() }).ToListAsync();
        var byOwner = counts.ToDictionary(x => (x.OwnerType, x.OwnerId), x => x.Count);
        var rows = await context.People.AsNoTracking().Where(p => p.MergedIntoPersonId == null)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id, p.LastName, p.FirstName, p.SecondName, p.Status, p.BirthDate,
                Alias = p.Aliases.Where(a => a.IsPrimary).Select(a => a.Value).FirstOrDefault()
            }).ToListAsync();
        var people = rows.Select(p => new PersonSummary(p.Id,
            p.Status == IdentityStatus.Identified
                ? PersonSearchFilters.JoinFullName(p.LastName, p.FirstName, p.SecondName)
                : p.Alias ?? $"Аноним #{p.Id}",
            p.Status, p.BirthDate, byOwner.GetValueOrDefault((MediaOwnerType.Person, p.Id)))).ToList();
        var companies = (await companyService.ListAsync(null)).Select(c => c with
            { MediaCount = byOwner.GetValueOrDefault((MediaOwnerType.Company, c.Id)) }).ToList();
        var events = (await eventService.ListAsync()).Select(e => e with
            { MediaCount = byOwner.GetValueOrDefault((MediaOwnerType.Event, e.Id)) }).ToList();
        var names = people.ToDictionary(p => (MediaOwnerType.Person, p.Id), p => p.DisplayName);
        foreach (var company in companies) names[(MediaOwnerType.Company, company.Id)] = company.Name;
        foreach (var ev in events) names[(MediaOwnerType.Event, ev.Id)] = ev.Title;
        var media = (await mediaService.GetAllMediaAsync()).Select(item => new SettingsMedia(
            item.Media, item.OwnerType, item.OwnerId, names.GetValueOrDefault((item.OwnerType, item.OwnerId), "Удалён"))).ToList();
        return new SettingsData(people, companies, events, await auditLog.GetRecentLogsAsync(100), media);
    }
}
