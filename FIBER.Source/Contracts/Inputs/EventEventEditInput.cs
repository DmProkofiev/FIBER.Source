using System.Text.Json.Serialization;

namespace FIBER.Source.Contracts.Inputs;

// Входные данные одной связи «событие ↔ событие» при редактировании.
// Id == null — новая связь; Id != null — существующая запись.
public record EventEventEditInput(
    int? Id,
    int SourceEventId,
    int TargetEventId,
    string RelationshipType,
    string Notes = "");
