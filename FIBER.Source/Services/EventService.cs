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

public sealed class EventService : IEventService
{
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    private readonly IMediaService _mediaService;
    private readonly IAuditLogService _auditLog;

    public EventService(
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

    private static string? BuildFullName(string? last, string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(first))
            return null;

        var parts = new[] { last, first, second }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(" ", parts);
    }

    private static void ValidateEventDate(DateTime? value)
    {
        if (!value.HasValue) return;
        if (value.Value.Date > DateTime.Today)
            throw new InvalidOperationException(
                $"Дата события не может быть позже {DateTime.Today:dd.MM.yyyy}");
    }

    private static async Task ValidateOrganizersAsync(FiberSourceDbContext context, EventInput input)
    {
        if (input.OrganizerCompanyId.HasValue)
        {
            var exists = await context.Companies.AnyAsync(c => c.Id == input.OrganizerCompanyId.Value);
            if (!exists)
                throw new InvalidOperationException(
                    $"Организатор-компания Id={input.OrganizerCompanyId} не найдена");
        }
        if (input.OrganizerPersonId.HasValue)
        {
            var exists = await context.People.AnyAsync(p => p.Id == input.OrganizerPersonId.Value);
            if (!exists)
                throw new InvalidOperationException(
                    $"Организатор-человек Id={input.OrganizerPersonId} не найден");
        }
    }

    private static async Task ValidateParticipantsAsync(FiberSourceDbContext context, EventInput input)
    {
        if (input.Participants.Count == 0) return;

        var personIds = new HashSet<int>();
        var companyIds = new HashSet<int>();

        foreach (var p in input.Participants)
        {
            var hasPerson = p.PersonId.HasValue;
            var hasCompany = p.CompanyId.HasValue;

            if (hasPerson == hasCompany)
                throw new InvalidOperationException(
                    "Участник должен быть либо человеком, либо компанией");

            if (hasPerson && !personIds.Add(p.PersonId!.Value))
                throw new InvalidOperationException($"Участник-человек Id={p.PersonId} указан дважды");

            if (hasCompany && !companyIds.Add(p.CompanyId!.Value))
                throw new InvalidOperationException($"Участник-компания Id={p.CompanyId} указана дважды");
        }

        if (personIds.Count > 0)
        {
            var found = await context.People.CountAsync(p => personIds.Contains(p.Id));
            if (found != personIds.Count)
                throw new InvalidOperationException("Один или несколько участников-людей не найдены");
        }
        if (companyIds.Count > 0)
        {
            var found = await context.Companies.CountAsync(c => companyIds.Contains(c.Id));
            if (found != companyIds.Count)
                throw new InvalidOperationException("Одна или несколько участников-компаний не найдены");
        }
    }

    public async Task<Event> CreateAsync(EventInput input)
    {
        ValidateEventDate(input.Date);

        await using var context = await _contextFactory.CreateDbContextAsync();
        await ValidateOrganizersAsync(context, input);
        await ValidateParticipantsAsync(context, input);

        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var ev = new Event
            {
                Title = input.Title.Trim(),
                Date = input.Date,
                Description = NullIfEmpty(input.Description),
                Notes = NullIfEmpty(input.Notes),
                OrganizerCompanyId = input.OrganizerCompanyId,
                OrganizerPersonId = input.OrganizerPersonId
            };
            context.Events.Add(ev);
            await context.SaveChangesAsync();

            foreach (var p in input.Participants)
            {
                context.EventParticipants.Add(new EventParticipant
                {
                    EventId = ev.Id,
                    PersonId = p.PersonId,
                    CompanyId = p.CompanyId,
                    Role = p.Role,
                    Notes = p.Notes ?? string.Empty
                });
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();
            await _auditLog.LogAsync("Event.Create", $"Id={ev.Id}");
            return ev;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Event?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Events.AsNoTracking()
            .Include(e => e.OrganizerCompany)
            .Include(e => e.OrganizerPerson!).ThenInclude(p => p.Aliases)
            .Include(e => e.Participants).ThenInclude(p => p.Person!).ThenInclude(p => p.Aliases)
            .Include(e => e.Participants).ThenInclude(p => p.Company)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IReadOnlyList<EventSummary>> ListAsync(string? query = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        IQueryable<Event> q = context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var t = query.Trim();
            q = q.Where(e => e.Title.Contains(t));
        }

        var raw = await q
            .OrderByDescending(e => e.Date.HasValue)
            .ThenByDescending(e => e.Date)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Date,
                OrganizerCompanyName = e.OrganizerCompany != null ? e.OrganizerCompany.Name : null,
                OrganizerPersonId = e.OrganizerPersonId,
                OrganizerPersonLastName = e.OrganizerPerson != null ? e.OrganizerPerson.LastName : null,
                OrganizerPersonFirstName = e.OrganizerPerson != null ? e.OrganizerPerson.FirstName : null,
                OrganizerPersonSecondName = e.OrganizerPerson != null ? e.OrganizerPerson.SecondName : null
            })
            .ToListAsync();

        if (raw.Count == 0)
            return Array.Empty<EventSummary>();

        var eventIds = raw.Select(r => r.Id).ToList();

        var participantCounts = await context.EventParticipants
            .AsNoTracking()
            .Where(p => eventIds.Contains(p.EventId))
            .GroupBy(p => p.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        var anonymousIds = raw
            .Where(r => r.OrganizerPersonId != null && r.OrganizerPersonLastName == null)
            .Select(r => r.OrganizerPersonId!.Value)
            .ToList();

        Dictionary<int, string> anonymousNames;
        if (anonymousIds.Count > 0)
        {
            anonymousNames = await context.PersonAliases
                .AsNoTracking()
                .Where(a => a.IsPrimary && anonymousIds.Contains(a.PersonId))
                .ToDictionaryAsync(a => a.PersonId, a => a.Value);
        }
        else
        {
            anonymousNames = new Dictionary<int, string>();
        }

        var result = new List<EventSummary>(raw.Count);
        foreach (var r in raw)
        {
            string? organizerPersonName = BuildFullName(
                r.OrganizerPersonLastName,
                r.OrganizerPersonFirstName,
                r.OrganizerPersonSecondName);

            if (organizerPersonName is null && r.OrganizerPersonId != null)
            {
                organizerPersonName = anonymousNames.TryGetValue(
                    r.OrganizerPersonId.Value, out var alias)
                    ? alias
                    : "Аноним " + r.OrganizerPersonId;
            }

            result.Add(new EventSummary(
                r.Id,
                r.Title,
                r.Date,
                r.OrganizerCompanyName,
                organizerPersonName,
                0,
                participantCounts.GetValueOrDefault(r.Id, 0)));
        }

        return result;
    }

    public async Task UpdateAsync(int id, EventInput input)
    {
        ValidateEventDate(input.Date);

        await using var context = await _contextFactory.CreateDbContextAsync();
        await ValidateOrganizersAsync(context, input);

        // Режим точечного CRUD связей (участники с Id): валидируем новый набор.
        if (input.ParticipantLinks is not null)
            await ValidateParticipantLinksAsync(context, input.ParticipantLinks);
        else
            await ValidateParticipantsAsync(context, input);

        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var ev = await context.Events
                .Include(e => e.Participants)
                .FirstOrDefaultAsync(e => e.Id == id)
                ?? throw new InvalidOperationException($"Событие Id={id} не найдено");

            ev.Title = input.Title.Trim();
            ev.Date = input.Date;
            ev.Description = NullIfEmpty(input.Description);
            ev.Notes = NullIfEmpty(input.Notes);
            ev.OrganizerCompanyId = input.OrganizerCompanyId;
            ev.OrganizerPersonId = input.OrganizerPersonId;

            if (input.ParticipantLinks is not null)
            {
                // CRUD связей «событие ↔ участник» без пересоздания существующих записей:
                // изменённые поля обновляются на месте, удалённые — удаляются, новые — добавляются.
                await SyncParticipantsAsync(context, ev, input.ParticipantLinks);
            }
            else
            {
                // Обратная совместимость (старый путь): полное пересоздание списка участников.
                context.EventParticipants.RemoveRange(ev.Participants);
                await context.SaveChangesAsync();

                foreach (var p in input.Participants)
                {
                    context.EventParticipants.Add(new EventParticipant
                    {
                        EventId = ev.Id,
                        PersonId = p.PersonId,
                        CompanyId = p.CompanyId,
                        Role = p.Role,
                        Notes = p.Notes ?? string.Empty
                    });
                }
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();
            await _auditLog.LogAsync("Event.Update", $"Id={id}");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task ValidateParticipantLinksAsync(
        FiberSourceDbContext context, IReadOnlyList<EventParticipantEditInput> links)
    {
        var personIds = new HashSet<int>();
        var companyIds = new HashSet<int>();

        foreach (var p in links)
        {
            var hasPerson = p.PersonId.HasValue;
            var hasCompany = p.CompanyId.HasValue;

            if (hasPerson == hasCompany)
                throw new InvalidOperationException(
                    "Участник должен быть либо человеком, либо компанией");

            if (hasPerson && !personIds.Add(p.PersonId!.Value))
                throw new InvalidOperationException($"Участник-человек Id={p.PersonId} указан дважды");

            if (hasCompany && !companyIds.Add(p.CompanyId!.Value))
                throw new InvalidOperationException($"Участник-компания Id={p.CompanyId} указана дважды");
        }

        if (personIds.Count > 0)
        {
            var found = await context.People.CountAsync(p => personIds.Contains(p.Id));
            if (found != personIds.Count)
                throw new InvalidOperationException("Один или несколько участников-людей не найдены");
        }
        if (companyIds.Count > 0)
        {
            var found = await context.Companies.CountAsync(c => companyIds.Contains(c.Id));
            if (found != companyIds.Count)
                throw new InvalidOperationException("Одна или несколько участников-компаний не найдены");
        }
    }

    // ===== Синхронизация связей «событие ↔ участник» (CRUD внутри Update) =====
    private static async Task SyncParticipantsAsync(
        FiberSourceDbContext context, Event ev, IReadOnlyList<EventParticipantEditInput> links)
    {
        // DELETE: отсутствующие во входных данных.
        var inputIds = links.Where(l => l.Id.HasValue).Select(l => l.Id!.Value).ToHashSet();
        foreach (var existing in ev.Participants.ToList())
        {
            if (!inputIds.Contains(existing.Id))
            {
                ev.Participants.Remove(existing);
                context.EventParticipants.Remove(existing);
            }
        }

        // UPDATE: роль/заметки существующих записей меняются на месте.
        foreach (var input in links.Where(l => l.Id.HasValue))
        {
            var link = ev.Participants.FirstOrDefault(p => p.Id == input.Id!.Value);
            if (link is null) continue;
            link.Role = input.Role;
            link.Notes = input.Notes ?? string.Empty;
        }

        // CREATE: новые участники.
        foreach (var input in links.Where(l => !l.Id.HasValue))
        {
            context.EventParticipants.Add(new EventParticipant
            {
                EventId = ev.Id,
                PersonId = input.PersonId,
                CompanyId = input.CompanyId,
                Role = input.Role,
                Notes = input.Notes ?? string.Empty
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        await using (var tx = await context.Database.BeginTransactionAsync())
        {
            try
            {
                var ev = await context.Events.FirstOrDefaultAsync(e => e.Id == id);
                if (ev is not null) context.Events.Remove(ev);
                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        await _mediaService.RemoveAllLinksForOwnerAsync(MediaOwnerType.Event, id);
        await _auditLog.LogAsync("Event.Delete", $"Id={id}");
    }
}