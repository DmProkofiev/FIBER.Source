namespace FIBER.Source.Contracts.Inputs;

public record CompanyInteractionEditInput(
    int? Id,
    int? TargetCompanyId,
    string InteractionType,
    string Notes = "");
