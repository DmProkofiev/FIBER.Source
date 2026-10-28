using System;
using System.Collections.Generic;
using System.Linq;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Models.Helpers;

public sealed record EventParticipantRoleOption(EventParticipantRole Value, string Display);

public static class EventParticipantRoles
{
    public static string DisplayName(EventParticipantRole role) => role switch
    {
        EventParticipantRole.Participant => "Участник",
        EventParticipantRole.Organizer => "Организатор",
        EventParticipantRole.CoOrganizer => "Соорганизатор",
        EventParticipantRole.Speaker => "Докладчик",
        EventParticipantRole.Guest => "Гость",
        EventParticipantRole.Observer => "Наблюдатель",
        EventParticipantRole.Sponsor => "Спонсор",
        EventParticipantRole.Target => "Объект события",
        EventParticipantRole.Witness => "Свидетель",
        EventParticipantRole.Victim => "Потерпевший",
        EventParticipantRole.Suspect => "Подозреваемый",
        EventParticipantRole.Other => "Другое",
        _ => role.ToString()
    };

    public static IReadOnlyList<EventParticipantRoleOption> AllOptions { get; } =
        Enum.GetValues<EventParticipantRole>()
            .Select(r => new EventParticipantRoleOption(r, DisplayName(r)))
            .ToList();
}