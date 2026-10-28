namespace FIBER.Source.Contracts.Inputs;

// Входные данные одной связи «компания → компания» при редактировании организации.
// TargetCompanyId == null — новая связь (цель выбирается в форме).
public record CompanyInteractionEditInput(
    int? TargetCompanyId,
    string InteractionType,
    string Notes = "");
