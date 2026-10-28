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
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public sealed class PersonService : IPersonService
{
    private const int SearchResultLimit = 200;

    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    private readonly ISearchStrategyFactory _searchFactory;
    private readonly IAuditLogService _auditLog;
    private readonly IMediaService _mediaService;

    public PersonService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        ISearchStrategyFactory searchFactory,
        IAuditLogService auditLog,
        IMediaService mediaService)
    {
        _contextFactory = contextFactory;
        _searchFactory = searchFactory;
        _auditLog = auditLog;
        _mediaService = mediaService;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateBirthDate(DateTime? value)
    {
        if (!value.HasValue) return;
        if (value.Value.Date > DateTime.Today)
            throw new InvalidOperationException(
                $"Дата рождения не может быть позже {DateTime.Today:dd.MM.yyyy}");
    }

    private static string BuildFullName(string last, string first, string? second)
    {
        var parts = new[] { last, first, second }.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts);
    }

    // Разбор строки ФИО в фамилию/имя/отчество: первое слово — фамилия, последнее — отчество (если слов >= 3)
    internal static void SplitFullName(string fullName, out string lastName, out string firstName, out string? secondName)
    {
        var parts = fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            lastName = parts[0];
            firstName = parts[0];
            secondName = null;
        }
        else if (parts.Length == 2)
        {
            lastName = parts[0];
            firstName = parts[1];
            secondName = null;
        }
        else
        {
            lastName = parts[0];
            secondName = parts[^1];
            firstName = string.Join(" ", parts[1..^1]);
        }
    }

    public async Task<Person> CreateIdentifiedAsync(PersonInput input)
    {
        if (string.IsNullOrWhiteSpace(input.LastName))
            throw new InvalidOperationException("Фамилия обязательна для идентифицированной личности.");
        if (string.IsNullOrWhiteSpace(input.FirstName))
            throw new InvalidOperationException("Имя обязательно для идентифицированной личности.");
        ValidateBirthDate(input.BirthDate);

        await using var context = await _contextFactory.CreateDbContextAsync();

        var person = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = input.LastName.Trim(),
            FirstName = input.FirstName.Trim(),
            SecondName = NullIfEmpty(input.SecondName),
            Gender = input.Gender,
            BirthDate = input.BirthDate,
            Passport = NullIfEmpty(input.Passport),
            Job = NullIfEmpty(input.Job),
            Phone = NullIfEmpty(input.Phone),
            City = NullIfEmpty(input.City),
            CountryCode = NullIfEmpty(input.CountryCode),
            Car = NullIfEmpty(input.Car),
            Mail = NullIfEmpty(input.Mail),
            SocialMedia = NullIfEmpty(input.SocialMedia),
            Address = NullIfEmpty(input.Address),
            Notes = NullIfEmpty(input.Notes)
        };
        context.People.Add(person);
        await context.SaveChangesAsync();

        if (input.Aliases is { Count: > 0 })
        {
            foreach (var a in input.Aliases.Where(x => !x.IsPrimary && !string.IsNullOrWhiteSpace(x.Value)))
            {
                context.PersonAliases.Add(new PersonAlias
                {
                    PersonId = person.Id,
                    Value = a.Value.Trim(),
                    Type = a.Type,
                    IsPrimary = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();
        }

        await _auditLog.LogAsync("Person.CreateIdentified", $"Id={person.Id}");
        return person;
    }

    public async Task<Person> CreateAnonymousAsync(AnonymousPersonInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PrimaryAlias))
            throw new InvalidOperationException("PrimaryAlias обязателен для неидентифицированной личности.");

        await using var context = await _contextFactory.CreateDbContextAsync();

        var person = new Person
        {
            Status = IdentityStatus.Anonymous,
            LastName = null,
            FirstName = null,
            SecondName = null,
            Gender = input.Gender,
            BirthDate = null,
            Passport = null,
            Job = NullIfEmpty(input.Job),
            Phone = NullIfEmpty(input.Phone),
            City = NullIfEmpty(input.City),
            CountryCode = NullIfEmpty(input.CountryCode),
            Car = NullIfEmpty(input.Car),
            Mail = NullIfEmpty(input.Mail),
            SocialMedia = NullIfEmpty(input.SocialMedia),
            Address = NullIfEmpty(input.Address),
            Notes = NullIfEmpty(input.Notes)
        };
        context.People.Add(person);
        await context.SaveChangesAsync();

        context.PersonAliases.Add(new PersonAlias
        {
            PersonId = person.Id,
            Value = input.PrimaryAlias.Trim(),
            Type = AliasType.Alias,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow
        });

        if (input.Aliases is { Count: > 0 })
        {
            foreach (var a in input.Aliases.Where(x => !x.IsPrimary && !string.IsNullOrWhiteSpace(x.Value)))
            {
                context.PersonAliases.Add(new PersonAlias
                {
                    PersonId = person.Id,
                    Value = a.Value.Trim(),
                    Type = a.Type,
                    IsPrimary = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Person.CreateAnonymous", $"Id={person.Id}");
        return person;
    }

    public async Task<Person> PromoteAsync(int personId, PromotePersonInput input)
    {
        if (string.IsNullOrWhiteSpace(input.LastName))
            throw new InvalidOperationException("Фамилия обязательна");
        if (string.IsNullOrWhiteSpace(input.FirstName))
            throw new InvalidOperationException("Имя обязательно");
        ValidateBirthDate(input.BirthDate);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var person = await context.People.Include(p => p.Aliases)
            .FirstOrDefaultAsync(p => p.Id == personId)
            ?? throw new InvalidOperationException($"Личность Id={personId} не найдена");

        if (person.Status == IdentityStatus.Identified)
            throw new InvalidOperationException("Личность уже идентифицирована");

        person.Status = IdentityStatus.Identified;
        person.LastName = input.LastName.Trim();
        person.FirstName = input.FirstName.Trim();
        person.SecondName = NullIfEmpty(input.SecondName);
        person.Gender = input.Gender;
        person.BirthDate = input.BirthDate;
        person.Passport = NullIfEmpty(input.Passport);

        foreach (var alias in person.Aliases) alias.IsPrimary = false;

        var fullName = BuildFullName(person.LastName, person.FirstName, person.SecondName);
        context.PersonAliases.Add(new PersonAlias
        {
            PersonId = person.Id,
            Value = fullName,
            Type = AliasType.LegalName,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Person.Promote", $"Id={person.Id}");
        return person;
    }

    public async Task UpdateAsync(int id, PersonInput input)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var person = await context.People
            .Include(p => p.Aliases)
            .Include(p => p.Relationships)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Личность Id={id} не найдена");

        if (person.Status == IdentityStatus.Anonymous)
            throw new InvalidOperationException("UpdateAsync применим только к идентифицированной персоне.");
        if (string.IsNullOrWhiteSpace(input.LastName))
            throw new InvalidOperationException("Фамилия обязательна.");
        if (string.IsNullOrWhiteSpace(input.FirstName))
            throw new InvalidOperationException("Имя обязательно.");
        ValidateBirthDate(input.BirthDate);

        person.LastName = input.LastName.Trim();
        person.FirstName = input.FirstName.Trim();
        person.SecondName = NullIfEmpty(input.SecondName);
        person.Gender = input.Gender;
        person.BirthDate = input.BirthDate;
        person.Passport = NullIfEmpty(input.Passport);
        person.Job = NullIfEmpty(input.Job);
        person.Phone = NullIfEmpty(input.Phone);
        person.City = NullIfEmpty(input.City);
        person.CountryCode = NullIfEmpty(input.CountryCode);
        person.Car = NullIfEmpty(input.Car);
        person.Mail = NullIfEmpty(input.Mail);
        person.SocialMedia = NullIfEmpty(input.SocialMedia);
        person.Address = NullIfEmpty(input.Address);
        person.Notes = NullIfEmpty(input.Notes);

        if (input.Aliases != null)
        {
            var extras = input.Aliases
                .Where(a => !a.IsPrimary && !string.IsNullOrWhiteSpace(a.Value))
                .ToList();

            await SyncAliasesAsync(context, person, extras);
        }

        // Связи с другими персонами: добавление / изменение / удаление.
        await SyncRelationshipsAsync(context, person, input.Relationships);

        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Person.Update", $"Id={person.Id}");
    }

    public async Task UpdateAnonymousAsync(int id, AnonymousPersonInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PrimaryAlias))
            throw new InvalidOperationException("PrimaryAlias обязателен для неидентифицированной личности.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var person = await context.People
            .Include(p => p.Aliases)
            .Include(p => p.Relationships)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Личность Id={id} не найдена");

        if (person.Status == IdentityStatus.Identified)
            throw new InvalidOperationException("UpdateAnonymousAsync применим только к неидентифицированной персоне.");

        person.LastName = null;
        person.FirstName = null;
        person.SecondName = null;
        person.Gender = input.Gender;
        person.BirthDate = null;
        person.Passport = null;
        person.Job = NullIfEmpty(input.Job);
        person.Phone = NullIfEmpty(input.Phone);
        person.City = NullIfEmpty(input.City);
        person.CountryCode = NullIfEmpty(input.CountryCode);
        person.Car = NullIfEmpty(input.Car);
        person.Mail = NullIfEmpty(input.Mail);
        person.SocialMedia = NullIfEmpty(input.SocialMedia);
        person.Address = NullIfEmpty(input.Address);
        person.Notes = NullIfEmpty(input.Notes);

        var currentPrimary = person.Aliases.FirstOrDefault(a => a.IsPrimary);
        var primary = new PersonAliasInput(
            Id: currentPrimary?.Id,
            Value: input.PrimaryAlias.Trim(),
            Type: AliasType.Alias,
            IsPrimary: true);

        var all = new List<PersonAliasInput> { primary };
        if (input.Aliases is { Count: > 0 })
            all.AddRange(input.Aliases.Where(a => !a.IsPrimary && !string.IsNullOrWhiteSpace(a.Value)));

        await SyncAliasesAsync(context, person, all);

        // Связи с другими персонами: добавление / изменение / удаление.
        await SyncRelationshipsAsync(context, person, input.Relationships);

        await context.SaveChangesAsync();
        await _auditLog.LogAsync("Person.UpdateAnonymous", $"Id={person.Id}");
    }

    public async Task DeleteAliasAsync(int personId, int aliasId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var person = await context.People.Include(p => p.Aliases)
            .FirstOrDefaultAsync(p => p.Id == personId)
            ?? throw new InvalidOperationException($"Личность Id={personId} не найдена");

        var alias = person.Aliases.FirstOrDefault(a => a.Id == aliasId);
        if (alias == null) return;

        if (!alias.IsPrimary)
        {
            context.PersonAliases.Remove(alias);
            await context.SaveChangesAsync();
        }
        else
        {
            var others = person.Aliases.Where(a => a.Id != aliasId).ToList();
            if (others.Count == 0)
                throw new InvalidOperationException(
                    "Нельзя удалить единственный псевдоним — у анонима должно быть хотя бы одно имя.");

            var newPrimary = others.OrderBy(a => a.CreatedAt).First();

            await using var tx = await context.Database.BeginTransactionAsync();
            try
            {
                context.PersonAliases.Remove(alias);
                await context.SaveChangesAsync();
                newPrimary.IsPrimary = true;
                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        await _auditLog.LogAsync("Person.DeleteAlias", $"PersonId={personId}, AliasId={aliasId}");
    }

    private static async Task SyncAliasesAsync(
        FiberSourceDbContext context, Person person, IReadOnlyList<PersonAliasInput> inputs)
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var inputIds = inputs.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();
            foreach (var existing in person.Aliases.ToList())
            {
                if (!inputIds.Contains(existing.Id))
                {
                    context.PersonAliases.Remove(existing);
                    person.Aliases.Remove(existing);
                }
            }

            foreach (var input in inputs.Where(a => a.Id.HasValue))
            {
                var alias = person.Aliases.FirstOrDefault(a => a.Id == input.Id!.Value);
                if (alias == null) continue;
                alias.Value = input.Value.Trim();
                alias.Type = input.Type;
            }

            foreach (var input in inputs.Where(a => !a.Id.HasValue && !string.IsNullOrWhiteSpace(a.Value)))
            {
                var alias = new PersonAlias
                {
                    PersonId = person.Id,
                    Value = input.Value.Trim(),
                    Type = input.Type,
                    IsPrimary = false,
                    CreatedAt = DateTime.UtcNow
                };
                context.PersonAliases.Add(alias);
                person.Aliases.Add(alias);
            }

            await context.SaveChangesAsync();

            var primaryInput = inputs.FirstOrDefault(a => a.IsPrimary);
            if (primaryInput != null)
            {
                foreach (var alias in person.Aliases) alias.IsPrimary = false;
                await context.SaveChangesAsync();

                var primary = person.Aliases.FirstOrDefault(a =>
                    (primaryInput.Id.HasValue && a.Id == primaryInput.Id.Value) ||
                    (!primaryInput.Id.HasValue && a.Value == primaryInput.Value.Trim()));

                if (primary != null)
                {
                    primary.IsPrimary = true;
                    await context.SaveChangesAsync();
                }
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<PersonSummary>> SearchAsync(
        string query, string? strategyKey = null, bool? isIdentified = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var strategy = _searchFactory.Resolve(strategyKey);

        var q = context.People.AsNoTracking().Where(p => p.MergedIntoPersonId == null);
        q = strategy.Apply(q, query);

        if (isIdentified.HasValue)
        {
            q = isIdentified.Value
                ? q.Where(p => p.Status == IdentityStatus.Identified)
                : q.Where(p => p.Status == IdentityStatus.Anonymous);
        }

        var rows = await q
            .OrderByDescending(p => p.CreatedAt)
            .Take(SearchResultLimit)
            .Select(p => new { p.Id, p.LastName, p.FirstName, p.SecondName, p.Status, p.BirthDate })
            .ToListAsync();

        if (rows.Count == 0) return Array.Empty<PersonSummary>();

        var ids = rows.Select(r => r.Id).ToList();

        var anonymousIds = rows.Where(r => r.LastName == null).Select(r => r.Id).ToList();
        var primaryAliases = anonymousIds.Count > 0
            ? await context.PersonAliases
                .AsNoTracking()
                .Where(a => a.IsPrimary && anonymousIds.Contains(a.PersonId))
                .ToDictionaryAsync(a => a.PersonId, a => a.Value)
            : new Dictionary<int, string>();

        var aliasCounts = await context.PersonAliases
            .AsNoTracking()
            .Where(a => !a.IsPrimary && ids.Contains(a.PersonId))
            .GroupBy(a => a.PersonId)
            .Select(g => new { PersonId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PersonId, x => x.Count);

        var mediaCounts = await context.MediaAttachmentLinks
            .AsNoTracking()
            .Where(l => l.OwnerType == MediaOwnerType.Person && ids.Contains(l.OwnerId))
            .GroupBy(l => l.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

        return rows.Select(r => new PersonSummary(
            r.Id,
            r.LastName != null && r.FirstName != null
                ? BuildFullName(r.LastName, r.FirstName, r.SecondName)
                : primaryAliases.GetValueOrDefault(r.Id, "Аноним #" + r.Id),
            r.Status,
            r.BirthDate,
            mediaCounts.GetValueOrDefault(r.Id, 0),
            aliasCounts.GetValueOrDefault(r.Id, 0)))
            .ToList();
    }

    public async Task<IReadOnlyList<Person>> GetAllExceptAsync(int excludeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.People.AsNoTracking()
            .Where(p => p.Id != excludeId && p.MergedIntoPersonId == null)
            .ToListAsync();
    }

    public async Task<Person?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.People
            .AsNoTracking()
            .Include(p => p.Country)
            .Include(p => p.Aliases)
            // Связи персоны: и прямые, и обратные (чтобы в редакторе были видны обе стороны).
            .Include(p => p.Relationships).ThenInclude(r => r.RelatedPerson!)
            .Include(p => p.ReverseRelationships).ThenInclude(r => r.Person!)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    // ===== Синхронизация связей «персона ↔ персона» (CRUD внутри Update) =====

    // Полная синхронизация исходящих связей персоны: новые добавляются (с зеркалом у
    // связанной персоны), изменённые обновляются, отсутствующие удаляются (вместе с зеркалом).
    private static async Task SyncRelationshipsAsync(
        FiberSourceDbContext context, Person person, IReadOnlyList<PersonRelationshipEditInput>? inputs)
    {
        if (inputs is null) return; // null — не трогать связи; пустой список — удалить все.

        foreach (var existing in person.Relationships.ToList())
        {
            if (!inputs.Any(i => i.Id == existing.Id))
            {
                person.Relationships.Remove(existing);
                context.PersonRelationships.Remove(existing);
            }
        }

        foreach (var input in inputs.Where(i => i.Id.HasValue))
        {
            var link = person.Relationships.FirstOrDefault(r => r.Id == input.Id!.Value);
            if (link is null) continue;

            var changedTarget = link.RelatedPersonId != input.RelatedPersonId;
            link.Kind = input.Kind;
            link.Notes = NullIfEmpty(input.Notes) ?? string.Empty;

            // Если цель связи изменилась — переносим и зеркальную запись.
            if (changedTarget)
            {
                var inverseKind = PersonRelationKinds.InverseOf(input.Kind);
                var oldInverse = context.PersonRelationships.FirstOrDefault(r =>
                    r.PersonId == link.RelatedPersonId &&
                    r.RelatedPersonId == person.Id &&
                    r.Kind == PersonRelationKinds.InverseOf(link.Kind));

                link.RelatedPersonId = input.RelatedPersonId;

                if (inverseKind != link.Kind)
                {
                    if (oldInverse is not null) context.PersonRelationships.Remove(oldInverse);

                    var newInverseExists = await context.PersonRelationships.AnyAsync(r =>
                        r.PersonId == input.RelatedPersonId &&
                        r.RelatedPersonId == person.Id &&
                        r.Kind == inverseKind);

                    if (!newInverseExists && inverseKind != PersonRelationKind.Other)
                    {
                        context.PersonRelationships.Add(new PersonRelationship
                        {
                            PersonId = input.RelatedPersonId,
                            RelatedPersonId = person.Id,
                            Kind = inverseKind,
                            Notes = string.Empty
                        });
                    }
                }
            }
        }

        foreach (var input in inputs.Where(i => !i.Id.HasValue))
        {
            if (input.RelatedPersonId == person.Id)
                throw new InvalidOperationException("Связь человека с самим собой невозможна");

            var exists = person.Relationships.Any(r =>
                r.RelatedPersonId == input.RelatedPersonId && r.Kind == input.Kind);
            if (exists) continue;

            context.PersonRelationships.Add(new PersonRelationship
            {
                PersonId = person.Id,
                RelatedPersonId = input.RelatedPersonId,
                Kind = input.Kind,
                Notes = NullIfEmpty(input.Notes) ?? string.Empty
            });

            // Зеркальная связь для связанной персоны (как в RelationService.AddPersonRelationshipAsync).
            var inverseKind = PersonRelationKinds.InverseOf(input.Kind);
            if (inverseKind == input.Kind || inverseKind == PersonRelationKind.Other) continue;

            var inverseExists = await context.PersonRelationships.AnyAsync(r =>
                r.PersonId == input.RelatedPersonId &&
                r.RelatedPersonId == person.Id &&
                r.Kind == inverseKind);
            if (inverseExists) continue;

            context.PersonRelationships.Add(new PersonRelationship
            {
                PersonId = input.RelatedPersonId,
                RelatedPersonId = person.Id,
                Kind = inverseKind,
                Notes = string.Empty
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var person = await context.People.FirstOrDefaultAsync(p => p.Id == id);
            if (person is not null) context.People.Remove(person);

            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        await _mediaService.RemoveAllLinksForOwnerAsync(MediaOwnerType.Person, id);
        await _auditLog.LogAsync("Person.Delete", $"Id={id}");
    }
}