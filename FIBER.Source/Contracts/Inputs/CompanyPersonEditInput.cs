using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

// Входные данные одной связи «организация ↔ личность» при редактировании.
// Id == null — новая связь; Id != null — существующая запись.
public record CompanyPersonEditInput(
    int? Id,
    int CompanyId,
    int PersonId,
    string Role,
    string Notes = "");
