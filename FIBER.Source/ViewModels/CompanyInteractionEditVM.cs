using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

// Редактируемая строка взаимодействия «компания → компания» в форме редактирования организации.
public partial class CompanyInteractionEditVM : ObservableObject
{
    // Целевая организация связи. У CompanyInteraction составной ключ
    // (SourceCompanyId, TargetCompanyId), отдельного Id нет — поэтому для
    // существующих исходящих связей TargetCompanyId является идентификатором записи.
    [ObservableProperty] private int? _targetCompanyId;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _interactionType = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    // Только для отображения: взаимодействие инициировано другой организацией (обратная сторона).
    public bool IsReverseOnly { get; set; }

    public CompanyInteractionEditInput ToInput() =>
        new(TargetCompanyId, InteractionType, Notes);

    public static CompanyInteractionEditVM FromModel(CompanyInteraction ci) => new()
    {
        TargetCompanyId = ci.TargetCompanyId,
        DisplayName = ci.TargetCompany?.Name ?? $"Организация #{ci.TargetCompanyId}",
        InteractionType = ci.InteractionType,
        Notes = ci.Notes,
        IsReverseOnly = false
    };

    public static CompanyInteractionEditVM FromReverse(CompanyInteraction ci) => new()
    {
        TargetCompanyId = null,
        DisplayName = ci.SourceCompany?.Name ?? $"Организация #{ci.SourceCompanyId}",
        InteractionType = ci.InteractionType,
        Notes = ci.Notes,
        IsReverseOnly = true
    };
}
