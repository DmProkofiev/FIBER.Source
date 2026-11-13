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

    // Полная синхронизация исходящих связей персоны. Одна запись хранит обе роли:
    // KindFrom — роль персоны, KindTo — роль связанной персоны. Зеркальные записи не создаются.
    private static async Task SyncRelationshipsAsync(
        FiberSourceDbContext context, Person person, IReadOnlyList<PersonRelationshipEditInput>? inputs)
    {
        if (inputs is null) return; // null — не трогать связи; пустой список — удалить все.

        // DELETE: прямые связи, отсутствующие во входных данных.
        foreach (var existing in person.Relationships.ToList())
        {
            if (inputs.Any(i => i.Id == existing.Id)) continue;

            person.Relationships.Remove(existing);
            context.PersonRelationships.Remove(existing);
        }

        // UPDATE + CREATE.
        foreach (var input in inputs)
        {
            var targetId = input.RelatedPersonId;
            if (targetId <= 0) continue;
            if (targetId == person.Id)
                throw new InvalidOperationException("Связь человека с самим собой невозможна");

            if (!await context.People.AnyAsync(p => p.Id == targetId))
                throw new InvalidOperationException($"Личность Id={targetId} не найдена");

            // Защита от дубля: связь в обратном направлении уже существует.
            bool reverseExists = await context.PersonRelationships.AnyAsync(r =>
                r.PersonId == targetId && r.RelatedPersonId == person.Id);
            if (reverseExists) continue;

            var link = input.Id is not null
                ? person.Relationships.FirstOrDefault(r => r.Id == input.Id.Value)
                : null;
            if (link is not null)
            {
                link.RelatedPersonId = targetId;
                link.KindFrom = input.KindFrom;
                link.KindTo = input.KindTo;
                link.Notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty;
                continue;
            }

            bool exists = person.Relationships.Any(r => r.RelatedPersonId == targetId);
            if (exists) continue;

            var newLink = new PersonRelationship
            {
                PersonId = person.Id,
                RelatedPersonId = targetId,
                KindFrom = input.KindFrom,
                KindTo = input.KindTo,
                Notes = StringHelpers.NullIfEmpty(input.Notes) ?? string.Empty
            };
            context.PersonRelationships.Add(newLink);
            person.Relationships.Add(newLink);
        }

        await context.SaveChangesAsync();
    }

    // ===== Синхронизация связей «персона ↔ организация» (CRUD внутри Update) =====

    private static async Task SyncCompanyPeopleAsync(
        FiberSourceDbContext context, Person person, IReadOnlyList<CompanyPersonEditInput>? inputs)
    {
        if (inputs is null) return;

        var sync = new LinkSynchronizer(context);
        await sync.SyncLinksAsync(
            existingLinks: person.CompanyPeople,
            inputs: inputs,
            ownerId: person.Id,
            getInputId: i => i.Id,
            getInputTargetId: i => i.CompanyId,
            getInputRole: i => i.PersonRole,
            getInputRole2: i => i.CompanyRole,
            getInputNotes: i => i.Notes,
            setLinkFields: (link, role1, role2, notes) =>
            {
                link.PersonRole = role1;
                link.CompanyRole = role2;
                link.Notes = notes;
            },
            targetExists: id => context.Companies.AnyAsync(c => c.Id == id),
            duplicateExists: (ownerId, targetId) => context.CompanyPeople.AnyAsync(cp =>
                cp.CompanyId == targetId && cp.PersonId == ownerId),
            createLink: targetId => new CompanyPerson
            {
                PersonId = person.Id,
                CompanyId = targetId
            });
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
                link.Role = input.Role.GetValueOrDefault();
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
                Role = input.Role.GetValueOrDefault(),
                Notes = input.Notes ?? string.Empty
            });
        }
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
                    // Удаляем связи «персона ↔ персона» (прямые и обратные).
                    var relationships = await context.PersonRelationships
                        .Where(r => r.PersonId == id || r.RelatedPersonId == id)
                        .ToListAsync();
                    context.PersonRelationships.RemoveRange(relationships);

                    // Удаляем участие персоны в событиях.
                    var participations = await context.EventParticipants
                        .Where(ep => ep.PersonId == id)
                        .ToListAsync();
                    context.EventParticipants.RemoveRange(participations);

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
