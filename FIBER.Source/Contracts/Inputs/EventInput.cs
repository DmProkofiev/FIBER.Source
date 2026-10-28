using System;
using System.Collections.Generic;

namespace FIBER.Source.Contracts.Inputs;

public record EventInput(
    string Title,
    DateTime? Date,
    string Description,
    string Notes,
    int? OrganizerCompanyId,
    int? OrganizerPersonId,
    IReadOnlyList<EventParticipantInput> Participants);