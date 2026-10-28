using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

// Входные данные одной связи «персона ↔ персона» при редактировании личности.
// Id == null — новая связь; Id != null — существующая (значение может быть изменено).
public record PersonRelationshipEditInput(
    int? Id,
    int RelatedPersonId,
    PersonRelationKind Kind,
    string Notes = "");
