using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.ViewModels;

// Строка «участник» в форме создания/редактирования события.
public partial class EventParticipantEditVM : ObservableObject
{
    public int? PersonId { get; set; }
    public int? CompanyId { get; set; }

    [ObservableProperty] private string _displayName = string.Empty;

    public bool IsCompany => CompanyId.HasValue;

    public EventParticipantRole Role { get; set; } = EventParticipantRole.Participant;
    public string Notes { get; set; } = string.Empty;

    public EventParticipantInput ToInput() => new(PersonId, CompanyId, Role, Notes);

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