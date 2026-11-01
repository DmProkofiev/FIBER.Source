using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

/// <summary>
/// Сервис первичного заполнения БД тестовыми данными.
/// Генерирует 100 личностей, организации, события со связями между ними.
/// Если в БД уже есть данные — сидинг пропускается (идемпотентно).
/// </summary>
public interface ISeedDataService
{
    /// <summary>Заполняет БД тестовыми данными, если она пуста.</summary>
    Task<SeedResult> SeedAsync();
}

/// <summary>Результат сидинга.</summary>
public sealed record SeedResult(
    int PersonsCreated,
    int CompaniesCreated,
    int EventsCreated,
    int RelationshipsCreated,
    int EventParticipantsCreated,
    bool Skipped);

public sealed class SeedDataService : ISeedDataService
{
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;
    private readonly IPersonService _personService;
    private readonly ICompanyService _companyService;
    private readonly IEventService _eventService;
    private readonly IRelationService _relationService;

    private static readonly Random Rng = new(42); // фиксированное зерно для воспроизводимости

    public SeedDataService(
        IDbContextFactory<FiberSourceDbContext> contextFactory,
        IPersonService personService,
        ICompanyService companyService,
        IEventService eventService,
        IRelationService relationService)
    {
        _contextFactory = contextFactory;
        _personService = personService;
        _companyService = companyService;
        _eventService = eventService;
        _relationService = relationService;
    }

    public async Task<SeedResult> SeedAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        bool hasPeople = await context.People.AnyAsync();
        bool hasCompanies = await context.Companies.AnyAsync();
        bool hasEvents = await context.Events.AnyAsync();

        if (hasPeople || hasCompanies || hasEvents)
            return new SeedResult(0, 0, 0, 0, 0, true);

        // ===== Создаём организации (30) =====
        var companyIds = new List<int>();
        for (int i = 0; i < 30; i++)
        {
            var regNum = $"{Rng.Next(10, 99)}{Rng.Next(100, 999)}{Rng.Next(1000, 9999)}";
            var company = await _companyService.CreateAsync(new CompanyInput(
                Name: SeedData.CompanyNames[i % SeedData.CompanyNames.Length] + (i >= SeedData.CompanyNames.Length ? $" #{i}" : ""),
                RegistrationNumber: regNum,
                Address: $"{SeedData.Streets[Rng.Next(SeedData.Streets.Length)]}, д. {Rng.Next(1, 100)}",
                Phone: $"+7 ({Rng.Next(900, 999)}) {Rng.Next(100, 999)}-{Rng.Next(10, 99)}-{Rng.Next(10, 99)}",
                Mail: $"info{i}@company{i}.ru",
                Website: $"https://company{i}.ru",
                Notes: "Тестовая организация, созданная сидингом.",
                Interactions: null));
            companyIds.Add(company.Id);
        }

        // ===== Создаём личности (100) =====
        var personIds = new List<int>();
        for (int i = 0; i < 100; i++)
        {
            bool isIdentified = Rng.NextDouble() > 0.3; // ~70% идентифицированных
            string last = SeedData.RussianLastNames[Rng.Next(SeedData.RussianLastNames.Length)];
            string first = SeedData.RussianFirstNames[Rng.Next(SeedData.RussianFirstNames.Length)];
            string? second = isIdentified ? SeedData.RussianSecondNames[Rng.Next(SeedData.RussianSecondNames.Length)] : null;
            DateTime? birthDate = isIdentified
                ? new DateTime(Rng.Next(1950, 2010), Rng.Next(1, 13), Rng.Next(1, 28))
                : null;
            string? passport = isIdentified ? $"{Rng.Next(10, 99)} {Rng.Next(100, 999)} {Rng.Next(100000, 999999)}" : null;
            string phone = $"+7 ({Rng.Next(900, 999)}) {Rng.Next(100, 999)}-{Rng.Next(10, 99)}-{Rng.Next(10, 99)}";

            if (isIdentified)
            {
                var person = await _personService.CreateIdentifiedAsync(new PersonInput(
                    LastName: last,
                    FirstName: first,
                    SecondName: second,
                    Gender: (Gender)Rng.Next(0, 3),
                    BirthDate: birthDate,
                    Passport: passport!,
                    Job: SeedData.Jobs[Rng.Next(SeedData.Jobs.Length)],
                    Phone: phone,
                    City: SeedData.Cities[Rng.Next(SeedData.Cities.Length)],
                    CountryCode: SeedData.CountryCodes[Rng.Next(SeedData.CountryCodes.Length)],
                    Car: Rng.NextDouble() > 0.7 ? $"Марка {Rng.Next(1, 20)}" : "",
                    Mail: $"{first.ToLower()}.{last.ToLower()}{i}@mail.ru",
                    SocialMedia: Rng.NextDouble() > 0.7 ? $"vk.com/user{i}" : "",
                    Address: $"{SeedData.Streets[Rng.Next(SeedData.Streets.Length)]}, д. {Rng.Next(1, 200)}, кв. {Rng.Next(1, 100)}",
                    Notes: "Тестовая личность, созданная сидингом."));
                personIds.Add(person.Id);
            }
            else
            {
                var alias = SeedData.Aliases[Rng.Next(SeedData.Aliases.Length)] + $"#{Rng.Next(10, 99)}";
                var person = await _personService.CreateAnonymousAsync(new AnonymousPersonInput(
                    PrimaryAlias: alias,
                    Notes: "Тестовый аноним, созданный сидингом.",
                    Gender: (Gender)Rng.Next(0, 3),
                    Job: SeedData.Jobs[Rng.Next(SeedData.Jobs.Length)],
                    Phone: phone,
                    City: SeedData.Cities[Rng.Next(SeedData.Cities.Length)],
                    CountryCode: SeedData.CountryCodes[Rng.Next(SeedData.CountryCodes.Length)],
                    Car: "",
                    Mail: $"{alias.ToLower()}@mail.ru",
                    SocialMedia: "",
                    Address: "",
                    Aliases: new List<PersonAliasInput>
                    {
                        new(null, SeedData.Aliases[Rng.Next(SeedData.Aliases.Length)], AliasType.Nickname, false),
                        new(null, SeedData.Aliases[Rng.Next(SeedData.Aliases.Length)], AliasType.Handle, false)
                    }));
                personIds.Add(person.Id);
            }
        }

        // ===== Связи Person <-> Person (около 80) =====
        var relationKinds = Enum.GetValues<PersonRelationKind>().Where(k => k != PersonRelationKind.Other).ToArray();
        int relationshipsCreated = 0;
        for (int i = 0; i < 80; i++)
        {
            int a = personIds[Rng.Next(personIds.Count)];
            int b = personIds[Rng.Next(personIds.Count)];
            if (a == b) continue;
            var kind = relationKinds[Rng.Next(relationKinds.Length)];
            try
            {
                await _relationService.AddPersonRelationshipAsync(a, b, kind, "Тестовая связь из сидинга.");
                relationshipsCreated++;
            }
            catch { /* дубликат или self-link — пропускаем */ }
        }

        // ===== Создаём события (30) =====
        var eventIds = new List<int>();
        for (int i = 0; i < 30; i++)
        {
            var eventDate = new DateTime(Rng.Next(2020, 2026), Rng.Next(1, 13), Rng.Next(1, 28));
            int? orgPersonId = personIds.Count > 0 && Rng.NextDouble() > 0.5 ? personIds[Rng.Next(personIds.Count)] : null;
            int? orgCompanyId = companyIds.Count > 0 && Rng.NextDouble() > 0.5 ? companyIds[Rng.Next(companyIds.Count)] : null;

            var participants = new List<EventParticipantInput>();
            int participantCount = Rng.Next(1, 6);
            for (int j = 0; j < participantCount; j++)
            {
                bool usePerson = Rng.NextDouble() > 0.3;
                if (usePerson && personIds.Count > 0)
                    participants.Add(new EventParticipantInput(personIds[Rng.Next(personIds.Count)], null, (EventParticipantRole)Rng.Next(0, 11), "Участник из сидинга."));
                else if (companyIds.Count > 0)
                    participants.Add(new EventParticipantInput(null, companyIds[Rng.Next(companyIds.Count)], (EventParticipantRole)Rng.Next(0, 11), "Организация из сидинга."));
            }

            var evt = await _eventService.CreateAsync(new EventInput(
                Title: SeedData.EventTitles[i % SeedData.EventTitles.Length] + (i >= SeedData.EventTitles.Length ? $" #{i}" : ""),
                Date: eventDate,
                Description: "Описание тестового события.",
                Notes: "Событие создано сидингом.",
                OrganizerCompanyId: orgCompanyId,
                OrganizerPersonId: orgPersonId,
                Participants: participants));
            eventIds.Add(evt.Id);
        }

        return new SeedResult(
            PersonsCreated: personIds.Count,
            CompaniesCreated: companyIds.Count,
            EventsCreated: eventIds.Count,
            RelationshipsCreated: relationshipsCreated,
            EventParticipantsCreated: eventIds.Sum(_ => Rng.Next(1, 6)),
            Skipped: false);
    }
}
