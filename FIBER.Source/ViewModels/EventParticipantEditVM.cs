using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;
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
    public int? EventId { get; set; }

    [ObservableProperty] private string _displayName = string.Empty;

    public bool IsCompany => CompanyId.HasValue;

    [ObservableProperty] private EventParticipantRole? _role;

    [ObservableProperty] private string _notes = string.Empty;

    public static IReadOnlyList<EventParticipantRoleOption> AvailableRoles { get; } =
        Enum.GetValues<EventParticipantRole>()
            .Select(r => new EventParticipantRoleOption(r, EventParticipantRoles.DisplayName(r)))
            .ToList();

    public EventParticipantInput ToInput() => new(PersonId, CompanyId, EventId, Role.GetValueOrDefault(), Notes);

    public EventParticipantEditInput ToEditInput() => new(Id, PersonId, CompanyId, EventId, Role, Notes);

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

    public static EventParticipantEditVM FromModel(EventParticipant ep) => new()
    {
        Id = ep.Id,
        PersonId = ep.PersonId,
        CompanyId = ep.CompanyId,
        EventId = ep.EventId,
        DisplayName = ep.Person != null
            ? (ep.Person.FullName ?? ep.Person.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value ?? $"Аноним #{ep.PersonId}")
            : (ep.Company?.Name ?? $"Организация #{ep.CompanyId}"),
        Role = ep.Role,
        Notes = ep.Notes
    };
}