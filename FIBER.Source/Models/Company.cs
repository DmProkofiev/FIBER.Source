using System.Collections.Generic;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.Models;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Mail { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }

    public ICollection<Event> OrganizedEvents { get; set; } = new List<Event>();
    public ICollection<EventParticipant> EventParticipations { get; set; } = new List<EventParticipant>();
    public ICollection<CompanyInteraction> OutgoingInteractions { get; set; } = new List<CompanyInteraction>();
    public ICollection<CompanyInteraction> IncomingInteractions { get; set; } = new List<CompanyInteraction>();
}