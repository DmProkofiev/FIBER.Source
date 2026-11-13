using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

public partial class CompanyInteractionEditVM : ObservableObject
{
    public int? Id { get; set; }

    [ObservableProperty] private int? _targetCompanyId;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _interactionType = string.Empty;
    [ObservableProperty] private string _interactionTypeTo = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public bool IsReverseOnly { get; set; }

    public CompanyInteractionEditInput ToInput() =>
        new(Id, TargetCompanyId, InteractionType, InteractionTypeTo, Notes);

    public static CompanyInteractionEditVM FromModel(CompanyInteraction ci) => new()
    {
        Id = ci.Id,
        TargetCompanyId = ci.TargetCompanyId,
        DisplayName = ci.TargetCompany?.Name ?? $"Организация #{ci.TargetCompanyId}",
        InteractionType = ci.InteractionType,
        InteractionTypeTo = ci.InteractionTypeTo,
        Notes = ci.Notes,
        IsReverseOnly = false
    };

    public static CompanyInteractionEditVM FromReverse(CompanyInteraction ci) => new()
    {
        Id = ci.Id,
        TargetCompanyId = ci.SourceCompanyId,
        DisplayName = ci.SourceCompany?.Name ?? $"Организация #{ci.SourceCompanyId}",
        InteractionType = ci.InteractionTypeTo,
        InteractionTypeTo = ci.InteractionType,
        Notes = ci.Notes,
        IsReverseOnly = true
    };
}
