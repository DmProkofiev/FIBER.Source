using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FIBER.Source.ViewModels;

// Строка «участник» в форме создания/редактирования события.
public partial class EventParticipantEditVM : ObservableObject
{
    // Id существующей записи EventParticipant; null — новый участник (точечный CRUD при редактировании).
    public int? Id { get; set; }

    public int? PersonId { get; set; }
    public int? CompanyId { get; set; }

    [ObservableProperty] private string _displayName = string.Empty;

    public bool IsCompany => CompanyId.HasValue;

    [ObservableProperty] private EventParticipantRole _role = EventParticipantRole.Participant;

    [ObservableProperty] private string _notes = string.Empty;

    public static IReadOnlyList<EventParticipantRoleOption> AvailableRoles { get; } =
        Enum.GetValues<EventParticipantRole>()
            .Select(r => new EventParticipantRoleOption(r, EventParticipantRoles.DisplayName(r)))
            .ToList();

    public EventParticipantInput ToInput() => new(PersonId, CompanyId, Role, Notes);

    public EventParticipantEditInput ToEditInput() => new(Id, PersonId, CompanyId, Role, Notes);

    public static EventParticipantEditVM ForPerson(int personId, string displayName) => new()
    {
        PersonId = personId,
        DisplayName = displayName
    };

    public static EventParticipantEditVM ForCompany(int companyId, string displayName) => new()
    {
        CompanyId = companyId,
        DisplayName = displayName
    };
}