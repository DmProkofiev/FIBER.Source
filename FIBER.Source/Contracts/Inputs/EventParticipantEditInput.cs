using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record EventParticipantEditInput(
    int? Id,
    int? PersonId,
    int? CompanyId,
    int? EventId,
    EventParticipantRole? Role,
    string Notes = "");
