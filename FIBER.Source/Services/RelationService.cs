using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public sealed class RelationService : IRelationService
{
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    private readonly IAuditLogService _auditLog;

    public RelationService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IAuditLogService auditLog)
    {
        _contextFactory = contextFactory;
        _auditLog = auditLog;
    }

    public async Task<IReadOnlyList<EventParticipant>> ListEventParticipantsAsync(int personId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.EventParticipants.AsNoTracking()
            .Include(ep => ep.Event)
            .Include(ep => ep.Person)
            .Where(ep => ep.PersonId == personId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<EventParticipant>> ListParticipantsByEventAsync(int eventId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.EventParticipants.AsNoTracking()
            .Include(ep => ep.Event)
            .Include(ep => ep.Person).ThenInclude(p => p!.Aliases)
            .Include(ep => ep.Company)
            .Where(ep => ep.EventId == eventId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<EventParticipant>> ListParticipantsByCompanyAsync(int companyId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.EventParticipants.AsNoTracking()
            .Include(ep => ep.Event)
            .Where(ep => ep.CompanyId == companyId)
            .ToListAsync();
    }

    public async Task AddEventParticipantAsync(int eventId, int? personId, int? companyId,
        EventParticipantRole role, string notes)
    {
        bool hasPerson = personId.HasValue;
        bool hasCompany = companyId.HasValue;
        if (hasPerson == hasCompany)
            throw new InvalidOperationException("Участник должен быть человеком ИЛИ компанией");

        await using var context = await _contextFactory.CreateDbContextAsync();

        if (!await context.Events.AnyAsync(e => e.Id == eventId))
            throw new InvalidOperationException($"Событие Id={eventId} не найдено");

        if (hasPerson && !await context.People.AnyAsync(p => p.Id == personId))
            throw new InvalidOperationException($"Личность Id={personId} не найдена");
        if (hasCompany && !await context.Companies.AnyAsync(c => c.Id == companyId))
            throw new InvalidOperationException($"Организация Id={companyId} не найдена");

        bool exists = hasPerson
            ? await context.EventParticipants.AnyAsync(ep => ep.EventId == eventId && ep.PersonId == personId)
            : await context.EventParticipants.AnyAsync(ep => ep.EventId == eventId && ep.CompanyId == companyId);
        if (exists) return;

        context.EventParticipants.Add(new EventParticipant
        {
            EventId = eventId,
            PersonId = personId,
            CompanyId = companyId,
            Role = role,
            Notes = notes ?? string.Empty
        });
        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Relation.AddEventParticipant", $"Event={eventId}, Role={role}");
    }

    public async Task RemoveEventParticipantAsync(int participantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var item = await context.EventParticipants.FirstOrDefaultAsync(ep => ep.Id == participantId);
        if (item is null) return;

        context.EventParticipants.Remove(item);
        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Relation.RemoveEventParticipant", $"Participant={participantId}");
    }

    public async Task<IReadOnlyList<PersonRelationship>> ListRelationshipsAsync(int personId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PersonRelationships.AsNoTracking()
            .Include(r => r.Person)
            .Include(r => r.RelatedPerson).ThenInclude(p => p.Aliases)
            .Where(r => r.PersonId == personId)
            .ToListAsync();
    }

    public async Task AddPersonRelationshipAsync(int personId, int relatedPersonId,
        PersonRelationKind kind, string notes)
    {
        if (personId == relatedPersonId)
            throw new InvalidOperationException("Связь человека с самим собой невозможна");

        await using var context = await _contextFactory.CreateDbContextAsync();

        if (!await context.People.AnyAsync(p => p.Id == personId))
            throw new InvalidOperationException($"Личность Id={personId} не найдена");
        if (!await context.People.AnyAsync(p => p.Id == relatedPersonId))
            throw new InvalidOperationException($"Личность Id={relatedPersonId} не найдена");

        bool exists = await context.PersonRelationships.AnyAsync(r =>
            r.PersonId == personId && r.RelatedPersonId == relatedPersonId && r.Kind == kind);
        if (exists) return;

        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            context.PersonRelationships.Add(new PersonRelationship
            {
                PersonId = personId,
                RelatedPersonId = relatedPersonId,
                Kind = kind,
                Notes = notes ?? string.Empty
            });

            var inverse = PersonRelationKinds.InverseOf(kind);
            if (inverse != kind && inverse != PersonRelationKind.Other)
            {
                bool inverseExists = await context.PersonRelationships.AnyAsync(r =>
                    r.PersonId == relatedPersonId && r.RelatedPersonId == personId && r.Kind == inverse);
                if (!inverseExists)
                {
                    context.PersonRelationships.Add(new PersonRelationship
                    {
                        PersonId = relatedPersonId,
                        RelatedPersonId = personId,
                        Kind = inverse,
                        Notes = string.Empty
                    });
                }
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();
            await _auditLog.LogAsync("Relation.AddPersonRelationship",
                $"Person={personId}, Related={relatedPersonId}, Kind={kind}");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task RemovePersonRelationshipAsync(int relationshipId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var item = await context.PersonRelationships.FirstOrDefaultAsync(r => r.Id == relationshipId);
        if (item is null) return;

        context.PersonRelationships.Remove(item);
        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Relation.RemovePersonRelationship", $"Relationship={relationshipId}");
    }

    public async Task<IReadOnlyList<CompanyInteraction>> ListCompanyInteractionsAsync(int companyId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.CompanyInteractions.AsNoTracking()
            .Include(ci => ci.SourceCompany)
            .Include(ci => ci.TargetCompany)
            .Where(ci => ci.SourceCompanyId == companyId)
            .ToListAsync();
    }

    public async Task AddCompanyInteractionAsync(int sourceCompanyId, int targetCompanyId,
        string interactionType, string notes)
    {
        if (sourceCompanyId == targetCompanyId) return;
        await using var context = await _contextFactory.CreateDbContextAsync();

        bool exists = await context.CompanyInteractions.AnyAsync(ci =>
            ci.SourceCompanyId == sourceCompanyId && ci.TargetCompanyId == targetCompanyId);
        if (exists) return;

        context.CompanyInteractions.Add(new CompanyInteraction
        {
            SourceCompanyId = sourceCompanyId,
            TargetCompanyId = targetCompanyId,
            InteractionType = interactionType ?? string.Empty,
            Notes = notes ?? string.Empty
        });
        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Relation.AddCompanyInteraction",
            $"Source={sourceCompanyId}, Target={targetCompanyId}");
    }

    public async Task RemoveCompanyInteractionAsync(int sourceCompanyId, int targetCompanyId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var item = await context.CompanyInteractions.FirstOrDefaultAsync(ci =>
            ci.SourceCompanyId == sourceCompanyId && ci.TargetCompanyId == targetCompanyId);
        if (item is null) return;

        context.CompanyInteractions.Remove(item);
        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Relation.RemoveCompanyInteraction",
            $"Source={sourceCompanyId}, Target={targetCompanyId}");
    }
}