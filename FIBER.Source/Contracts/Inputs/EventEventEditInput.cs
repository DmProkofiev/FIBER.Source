using System.Text.Json.Serialization;

namespace FIBER.Source.Contracts.Inputs;

public record EventEventEditInput(
    int? Id,
    int SourceEventId,
    int TargetEventId,
    string RelationshipType,
    string Notes = "");
