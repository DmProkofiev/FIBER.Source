using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record PersonRelationshipEditInput(
    int? Id,
    int RelatedPersonId,
    PersonRelationKind? Kind,
    string Notes = "");
