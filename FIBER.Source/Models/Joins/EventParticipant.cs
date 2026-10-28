using System.ComponentModel.DataAnnotations.Schema;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;

namespace FIBER.Source.Models.Joins;

public class EventParticipant
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public EventParticipantRole Role { get; set; } = EventParticipantRole.Participant;
    public string Notes { get; set; } = string.Empty;

    [NotMapped]
    public string RoleDisplay => EventParticipantRoles.DisplayName(Role);
}