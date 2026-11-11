using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

// Редактируемая строка взаимодействия «компания → компания» в форме редактирования организации.
public partial class CompanyInteractionEditVM : ObservableObject
{
    // Id существующей записи CompanyInteraction; null — новая связь.
    public int? Id { get; set; }

    [ObservableProperty] private int? _targetCompanyId;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _interactionType = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    // Только для отображения: взаимодействие инициировано другой организацией (обратная сторона).
    public bool IsReverseOnly { get; set; }

    public CompanyInteractionEditInput ToInput() =>
        new(Id, TargetCompanyId, InteractionType, Notes);

    public static CompanyInteractionEditVM FromModel(CompanyInteraction ci) => new()
    {
        Id = ci.Id,
        TargetCompanyId = ci.TargetCompanyId,
        DisplayName = ci.TargetCompany?.Name ?? $"Организация #{ci.TargetCompanyId}",
        InteractionType = ci.InteractionType,
        Notes = ci.Notes,
        IsReverseOnly = false
    };

    public static CompanyInteractionEditVM FromReverse(CompanyInteraction ci) => new()
    {
        Id = null,
        TargetCompanyId = null,
        DisplayName = ci.SourceCompany?.Name ?? $"Организация #{ci.SourceCompanyId}",
        InteractionType = ci.InteractionType,
        Notes = ci.Notes,
        IsReverseOnly = true
    };
}
