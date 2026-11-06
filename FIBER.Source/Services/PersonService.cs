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
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public sealed class PersonService : BaseService, IPersonService
{
    private const int SearchResultLimit = 200;

    private readonly ISearchStrategyFactory _searchFactory;

    public PersonService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        ISearchStrategyFactory searchFactory,
        IAuditLogService auditLog,
        IMediaService mediaService)
        : base(contextFactory, mediaService, auditLog)
    {
        _searchFactory = searchFactory;
    }

    private static void ValidateBirthDate(DateTime? value)
    {
        if (!value.HasValue) return;
        if (value.Value.Date > DateTime.Today)
            throw new InvalidOperationException(
                $"Дата рождения не может быть позже {DateTime.Today:dd.MM.yyyy}");
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
            SecondName = StringHelpers.NullIfEmpty(input.SecondName),
            Gender = input.Gender,
            BirthDate = input.BirthDate,
            Passport = StringHelpers.NullIfEmpty(input.Passport),
            Job = StringHelpers.NullIfEmpty(input.Job),
            Phone = StringHelpers.NullIfEmpty(input.Phone),
            City = StringHelpers.NullIfEmpty(input.City),
            CountryCode = StringHelpers.NullIfEmpty(input.CountryCode),
            Car = StringHelpers.NullIfEmpty(input.Car),
            Mail = StringHelpers.NullIfEmpty(input.Mail),
            SocialMedia = StringHelpers.NullIfEmpty(input.SocialMedia),
            Address = StringHelpers.NullIfEmpty(input.Address),
            Notes = StringHelpers.NullIfEmpty(input.Notes)
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

        // Связи с другими персонами при создании.
        if (input.Relationships is { Count: > 0 })
        {
            await SyncRelationshipsAsync(context, person, input.Relationships);
        }

        // Связи с организациями при создании.
        if (input.CompanyPeople is { Count: > 0 })
        {
            await SyncCompanyPeopleAsync(context, person, input.CompanyPeople);
        }

        // Связи с событиями при создании.
        if (input.EventParticipations is { Count: > 0 })
        {
            await SyncEventParticipationsAsync(context, person, input.EventParticipations);
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
            Job = StringHelpers.NullIfEmpty(input.Job),
            Phone = StringHelpers.NullIfEmpty(input.Phone),
            City = StringHelpers.NullIfEmpty(input.City),
            CountryCode = StringHelpers.NullIfEmpty(input.CountryCode),
            Car = StringHelpers.NullIfEmpty(input.Car),
            Mail = StringHelpers.NullIfEmpty(input.Mail),
            SocialMedia = StringHelpers.NullIfEmpty(input.SocialMedia),
            Address = StringHelpers.NullIfEmpty(input.Address),
            Notes = StringHelpers.NullIfEmpty(input.Notes)
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

        // Связи с другими персонами при создании.
        if (input.Relationships is { Count: > 0 })
        {
            await SyncRelationshipsAsync(context, person, input.Relationships);
        }

        // Связи с организациями при создании.
        if (input.CompanyPeople is { Count: > 0 })
        {
            await SyncCompanyPeopleAsync(context, person, input.CompanyPeople);
        }

        // Связи с событиями при создании.
        if (input.EventParticipations is { Count: > 0 })
        {
            await SyncEventParticipationsAsync(context, person, input.EventParticipations);
        }

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
        person.SecondName = StringHelpers.NullIfEmpty(input.SecondName);
        person.Gender = input.Gender;
        person.BirthDate = input.BirthDate;
        person.Passport = StringHelpers.NullIfEmpty(input.Passport);

        foreach (var alias in person.Aliases) alias.IsPrimary = false;

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
            // Связи персоны с организациями (для редактирования в Update-экране).
            .Include(p => p.CompanyPeople).ThenInclude(cp => cp.Company)
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
        person.SecondName = StringHelpers.NullIfEmpty(input.SecondName);
        person.Gender = input.Gender;
        person.BirthDate = input.BirthDate;
        person.Passport = StringHelpers.NullIfEmpty(input.Passport);
        person.Job = StringHelpers.NullIfEmpty(input.Job);
        person.Phone = StringHelpers.NullIfEmpty(input.Phone);
        person.City = StringHelpers.NullIfEmpty(input.City);
        person.CountryCode = StringHelpers.NullIfEmpty(input.CountryCode);
        person.Car = StringHelpers.NullIfEmpty(input.Car);
        person.Mail = StringHelpers.NullIfEmpty(input.Mail);
        person.SocialMedia = StringHelpers.NullIfEmpty(input.SocialMedia);
        person.Address = StringHelpers.NullIfEmpty(input.Address);
        person.Notes = StringHelpers.NullIfEmpty(input.Notes);

        // Псевдонимы идентифицированной личности не создаются автоматически:
        // ФИО хранится в LastName/FirstName/SecondName, а Aliases — только явно добавленные имена.
        // null — не трогать псевдонимы; список (в т.ч. пустой) — полная синхронизация.
        if (input.Aliases != null)
        {
            // У идентифицированной личности псевдонимы не основные — флаг IsPrimary не используется.
            foreach (var alias in person.Aliases) alias.IsPrimary = false;

            var aliases = input.Aliases
                .Where(a => !string.IsNullOrWhiteSpace(a.Value))
                .Select(a => new PersonAliasInput(a.Id, a.Value, a.Type, IsPrimary: false))
                .ToList();

            await SyncAliasesAsync(context, person, aliases);
        }

        // Связи с другими персонами: добавление / изменение / удаление.
        await SyncRelationshipsAsync(context, person, input.Relationships);

        // Связи с организациями: добавление / изменение / удаление.
        await SyncCompanyPeopleAsync(context, person, input.CompanyPeople);

        // Связи с событиями: добавление / изменение / удаление.
        await SyncEventParticipationsAsync(context, person, input.EventParticipations);

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
            // Связи персоны с организациями (для редактирования в Update-экране).
            .Include(p => p.CompanyPeople).ThenInclude(cp => cp.Company)
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
        person.Job = StringHelpers.NullIfEmpty(input.Job);
        person.Phone = StringHelpers.NullIfEmpty(input.Phone);
        person.City = StringHelpers.NullIfEmpty(input.City);
        person.CountryCode = StringHelpers.NullIfEmpty(input.CountryCode);
        person.Car = StringHelpers.NullIfEmpty(input.Car);
        person.Mail = StringHelpers.NullIfEmpty(input.Mail);
        person.SocialMedia = StringHelpers.NullIfEmpty(input.SocialMedia);
        person.Address = StringHelpers.NullIfEmpty(input.Address);
        person.Notes = StringHelpers.NullIfEmpty(input.Notes);

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

        // Связи с организациями: добавление / изменение / удаление.
        await SyncCompanyPeopleAsync(context, person, input.CompanyPeople);

        // Связи с событиями: добавление / изменение / удаление.
        await SyncEventParticipationsAsync(context, person, input.EventParticipations);

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

        if (person.Status == IdentityStatus.Identified)
        {
            // У идентифицированной личности псевдонимы необязательны:
            // удаляем без назначения нового основного (ФИО хранится в полях персоны).
            context.PersonAliases.Remove(alias);
            await context.SaveChangesAsync();
        }
        else if (!alias.IsPrimary)
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

        // Анонимом считается запись со статусом Anonymous (не по пустой фамилии!).
        var anonymousIds = rows.Where(r => r.Status == IdentityStatus.Anonymous).Select(r => r.Id).ToList();
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
            // Имя отображаем по статусу; ФИО склеиваем из всех непустых компонентов
            // (LastName + FirstName + SecondName), в том числе если заполнено только одно из них.
            r.Status == IdentityStatus.Identified
                ? FIBER.Source.Services.Search.PersonSearchFilters
                    .JoinFullName(r.LastName, r.FirstName, r.SecondName)
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
            // Псевдонимы нужны стратегиям сравнения (Levenshtein для анонимов).
            .Include(p => p.Aliases)
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
            // Псевдонимы нужны для отображения имени анонимной связанной личности.
            .Include(p => p.Relationships).ThenInclude(r => r.RelatedPerson!).ThenInclude(p => p.Aliases)
            .Include(p => p.ReverseRelationships).ThenInclude(r => r.Person!).ThenInclude(p => p.Aliases)
            // Связи персоны с организациями (для отображения и редактирования в Update-экране).
            .Include(p => p.CompanyPeople).ThenInclude(cp => cp.Company)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    // ===== Синхронизация связей «персона ↔ персона» (CRUD внутри Update) =====

    // Полная синхронизация исходящих связей персоны: новые добавляются (с зеркалом у
    // связанной персоны), изменённые обновляются (зеркало приводится в соответствие),
    // отсутствующие удаляются (вместе с зеркалом).
    private static async Task SyncRelationshipsAsync(
        FiberSourceDbContext context, Person person, IReadOnlyList<PersonRelationshipEditInput>? inputs)
    {
        if (inputs is null) return; // null — не трогать связи; пустой список — удалить все.

        // DELETE: прямые связи, отсутствующие во входных данных, и их зеркальные записи.
        foreach (var existing in person.Relationships.ToList())
        {
            if (inputs.Any(i => i.Id == existing.Id)) continue;

            person.Relationships.Remove(existing);
            context.PersonRelationships.Remove(existing);
            await RemoveInverseAsync(context, person.Id, existing.RelatedPersonId, existing.Kind);
        }

        // UPDATE: существующие связи — цель/вид/заметки; зеркало пересоздаётся при изменении.
        foreach (var input in inputs.Where(i => i.Id.HasValue))
        {
            var link = person.Relationships.FirstOrDefault(r => r.Id == input.Id!.Value);
            if (link is null) continue;

            if (input.RelatedPersonId == person.Id)
                throw new InvalidOperationException("Связь человека с самим собой невозможна");

            var oldRelatedId = link.RelatedPersonId;
            var oldKind = link.Kind;
            bool targetChanged = oldRelatedId != input.RelatedPersonId;
            bool kindChanged = oldKind != input.Kind;

            if (!targetChanged && !kindChanged)
            {
                link.Notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty;
                continue;
            }

            // Старое зеркало удаляем только если оно действительно станет неактуальным.
            var oldInverse = PersonRelationKinds.InverseOf(oldKind);
            var newInverse = PersonRelationKinds.InverseOf(input.Kind);
            bool oldHasMirror = oldInverse != oldKind && oldInverse != PersonRelationKind.Other;
            bool newHasMirror = newInverse != input.Kind && newInverse != PersonRelationKind.Other;

            if (oldHasMirror &&
                (targetChanged || !newHasMirror || oldInverse != newInverse))
            {
                var oldMirror = await context.PersonRelationships.FirstOrDefaultAsync(r =>
                    r.PersonId == oldRelatedId &&
                    r.RelatedPersonId == person.Id &&
                    r.Kind == oldInverse);
                if (oldMirror is not null) context.PersonRelationships.Remove(oldMirror);
            }

            link.RelatedPersonId = input.RelatedPersonId;
            link.Kind = input.Kind;
            link.Notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty;

            if (newHasMirror &&
                (targetChanged || !oldHasMirror || oldInverse != newInverse))
            {
                await EnsureInverseAsync(context, person.Id, input.RelatedPersonId, newInverse);
            }
        }

        // CREATE: новые прямые связи и их зеркала.
        foreach (var input in inputs.Where(i => !i.Id.HasValue))
        {
            if (input.RelatedPersonId == person.Id)
                throw new InvalidOperationException("Связь человека с самим собой невозможна");

            var exists = person.Relationships.Any(r =>
                r.RelatedPersonId == input.RelatedPersonId && r.Kind == input.Kind);
            if (exists) continue;

            var link = new PersonRelationship
            {
                PersonId = person.Id,
                RelatedPersonId = input.RelatedPersonId,
                Kind = input.Kind,
                Notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty
            };
            context.PersonRelationships.Add(link);
            person.Relationships.Add(link);

            var inverseKind = PersonRelationKinds.InverseOf(input.Kind);
            if (inverseKind == input.Kind || inverseKind == PersonRelationKind.Other) continue;

            await EnsureInverseAsync(context, person.Id, input.RelatedPersonId, inverseKind);
        }

        await context.SaveChangesAsync();
    }

    // ===== Синхронизация связей «персона ↔ организация» (CRUD внутри Update) =====

    // Полная синхронизация связей персоны с организациями: новые добавляются,
    // изменённые обновляются, отсутствующие удаляются.
    private static async Task SyncCompanyPeopleAsync(
        FiberSourceDbContext context, Person person, IReadOnlyList<CompanyPersonEditInput>? inputs)
    {
        if (inputs is null) return;

        // DELETE: связи, отсутствующие во входных данных.
        foreach (var existing in person.CompanyPeople?.ToList() ?? new List<CompanyPerson>())
        {
            if (inputs.Any(i => i.Id == existing.Id)) continue;

            context.CompanyPeople.Remove(existing);
        }

        // UPDATE + CREATE.
        // Для новых связей (Id == null) PersonId не задан — используем person.Id из контекста.
        foreach (var input in inputs.Where(i => i.CompanyId > 0))
        {
            var inputPersonId = input.Id.HasValue ? input.PersonId : person.Id;
            if (inputPersonId != person.Id)
                throw new InvalidOperationException("Невозможно связать персону с организацией от имени другого лица");

            if (!await context.Companies.AnyAsync(c => c.Id == input.CompanyId))
                throw new InvalidOperationException($"Организация Id={input.CompanyId} не найдена");

            var link = person.CompanyPeople?.FirstOrDefault(cp => cp.Id == input.Id);
            if (link is not null)
            {
                // Существующая связь — обновляем роль/заметки.
                link.Role = StringHelpers.NullIfEmpty(input.Role) ?? string.Empty;
                link.Notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty;
                continue;
            }

            // Защита от дубля.
            bool exists = await context.CompanyPeople.AnyAsync(cp =>
                cp.CompanyId == input.CompanyId && cp.PersonId == person.Id);
            if (exists) continue;

            context.CompanyPeople.Add(new CompanyPerson
            {
                CompanyId = input.CompanyId,
                PersonId = person.Id,
                Role = StringHelpers.NullIfEmpty(input.Role) ?? string.Empty,
                Notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty
            });
        }

        await context.SaveChangesAsync();
    }

    // ===== Синхронизация связей «персона ↔ событие» (CRUD внутри Update) =====

    private static async Task SyncEventParticipationsAsync(
        FiberSourceDbContext context, Person person, IReadOnlyList<EventParticipantEditInput>? inputs)
    {
        if (inputs is null) return;

        // DELETE: связи, отсутствующие во входных данных.
        foreach (var existing in person.EventParticipations?.ToList() ?? new List<EventParticipant>())
        {
            if (inputs.Any(i => i.Id == existing.Id)) continue;
            context.EventParticipants.Remove(existing);
        }

        // UPDATE + CREATE.
        foreach (var input in inputs.Where(i => i.EventId.HasValue))
        {
            var eventId = input.EventId!.Value;
            if (!await context.Events.AnyAsync(e => e.Id == eventId))
                throw new InvalidOperationException($"Событие Id={eventId} не найдено");

            var link = person.EventParticipations?.FirstOrDefault(ep => ep.Id == input.Id);
            if (link is not null)
            {
                link.Role = input.Role;
                link.Notes = input.Notes ?? string.Empty;
                continue;
            }

            // Защита от дубля.
            bool exists = await context.EventParticipants.AnyAsync(ep =>
                ep.EventId == eventId && ep.PersonId == person.Id);
            if (exists) continue;

            context.EventParticipants.Add(new EventParticipant
            {
                EventId = eventId,
                PersonId = person.Id,
                Role = input.Role,
                Notes = input.Notes ?? string.Empty
            });
        }

        await context.SaveChangesAsync();
    }

    // Добавляет зеркальную связь (related → person), если её ещё нет.
    private static async Task EnsureInverseAsync(
        FiberSourceDbContext context, int personId, int relatedPersonId, PersonRelationKind inverseKind)
    {
        bool exists = await context.PersonRelationships.AnyAsync(r =>
            r.PersonId == relatedPersonId &&
            r.RelatedPersonId == personId &&
            r.Kind == inverseKind);
        if (exists) return;

        context.PersonRelationships.Add(new PersonRelationship
        {
            PersonId = relatedPersonId,
            RelatedPersonId = personId,
            Kind = inverseKind,
            Notes = string.Empty
        });
    }

    // Удаляет зеркальную связь (related → person) для указанного вида прямой связи.
    private static async Task RemoveInverseAsync(
        FiberSourceDbContext context, int personId, int relatedPersonId, PersonRelationKind kind)
    {
        var inverseKind = PersonRelationKinds.InverseOf(kind);
        if (inverseKind == kind || inverseKind == PersonRelationKind.Other) return;

        var mirror = await context.PersonRelationships.FirstOrDefaultAsync(r =>
            r.PersonId == relatedPersonId &&
            r.RelatedPersonId == personId &&
            r.Kind == inverseKind);
        if (mirror is not null) context.PersonRelationships.Remove(mirror);
    }

    public async Task DeleteAsync(int id)
    {
        await DeleteWithMediaAsync(
            id,
            MediaOwnerType.Person,
            "Person.Delete",
            async context =>
            {
                var person = await context.People.FirstOrDefaultAsync(p => p.Id == id);
                if (person is not null)
                {
                    // Удаляем связи «персона ↔ организация».
                    var companyPeople = await context.CompanyPeople
                        .Where(cp => cp.PersonId == id)
                        .ToListAsync();
                    context.CompanyPeople.RemoveRange(companyPeople);

                    context.People.Remove(person);
                }
            });
    }
}