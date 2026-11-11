namespace FIBER.Source.Contracts.Inputs;

// Входные данные одной связи «компания → компания» при редактировании организации.
// Id == null — новая связь; Id != null — существующая запись.
public record CompanyInteractionEditInput(
    int? Id,
    int? TargetCompanyId,
    string InteractionType,
    string Notes = "");
