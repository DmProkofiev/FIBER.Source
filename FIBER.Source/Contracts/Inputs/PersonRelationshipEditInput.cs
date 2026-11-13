using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record PersonRelationshipEditInput(
    int? Id,
    int RelatedPersonId,
    PersonRelationKind KindFrom,
    PersonRelationKind KindTo,
    string Notes = "");
