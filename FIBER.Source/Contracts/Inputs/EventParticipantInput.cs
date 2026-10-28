using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record EventParticipantInput(
    int? PersonId,
    int? CompanyId,
    EventParticipantRole Role,
    string Notes = "");