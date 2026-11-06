using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record EventParticipantInput(
    int? PersonId,
    int? CompanyId,
    int? EventId,
    EventParticipantRole Role,
    string Notes = "");