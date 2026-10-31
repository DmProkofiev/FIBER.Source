using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.Models;

public class Person
{
    public int Id { get; set; }
    public IdentityStatus Status { get; set; } = IdentityStatus.Anonymous;

    public string? LastName { get; set; } // фамилия
    public string? FirstName { get; set; } // имя
    public string? SecondName { get; set; } // отчество

    public Gender Gender { get; set; } = Gender.Unknown;

    public DateTime? BirthDate { get; set; }
    public string? Passport { get; set; }
    public string? Job { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? Car { get; set; }
    public string? Mail { get; set; }
    public string? SocialMedia { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? MergedIntoPersonId { get; set; }

    public Country? Country { get; set; }
    public Person? MergedInto { get; set; }

    public ICollection<PersonAlias> Aliases { get; set; } = new List<PersonAlias>();
    public ICollection<EventParticipant> EventParticipations { get; set; } = new List<EventParticipant>();
    public ICollection<Event> OrganizerEvents { get; set; } = new List<Event>();
    // Прямые связи: данная персона — инициатор («Person» в PersonRelationship).
    public ICollection<PersonRelationship> Relationships { get; set; } = new List<PersonRelationship>();
    // Связи, где данная персона является «связанной» (обратная сторона связи другой персоны).
    public ICollection<PersonRelationship> ReverseRelationships { get; set; } = new List<PersonRelationship>();

    [NotMapped]
    public string? FullName =>
        Status == IdentityStatus.Identified && !string.IsNullOrWhiteSpace(LastName)
            ? string.Join(" ",
                new[] { LastName, FirstName, SecondName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            : null;

    [NotMapped]
    public string BirthDateDisplay => BirthDate?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";

    [NotMapped]
    public string GenderDisplay => Gender switch
    {
        Enums.Gender.Male => "Мужской",
        Enums.Gender.Female => "Женский",
        _ => "Сведения отсутствуют"
    };
}