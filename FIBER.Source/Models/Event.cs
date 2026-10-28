using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.Models;

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    // Организаторы (независимы, оба nullable)
    public int? OrganizerCompanyId { get; set; }
    public Company? OrganizerCompany { get; set; }

    public int? OrganizerPersonId { get; set; }
    public Person? OrganizerPerson { get; set; }

    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();

    [NotMapped]
    public string DateDisplay => Date?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";

    [NotMapped]
    public int ParticipantCount => Participants?.Count ?? 0;

    [NotMapped]
    public string OrganizerDisplay
    {
        get
        {
            var company = OrganizerCompany?.Name;
            var person = OrganizerPerson?.FullName
                ?? OrganizerPerson?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
                ?? (OrganizerPerson != null ? $"Аноним #{OrganizerPerson.Id}" : null);

            if (!string.IsNullOrWhiteSpace(company) && !string.IsNullOrWhiteSpace(person))
                return $"{company} ({person})";
            if (!string.IsNullOrWhiteSpace(company)) return company;
            if (!string.IsNullOrWhiteSpace(person)) return person;
            return "Сведения отсутствуют";
        }
    }
}