using System.ComponentModel.DataAnnotations.Schema;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.Models.Joins;

/// <summary>Прямая связь «событие ↔ событие».</summary>
public class EventEvent
{
    public int Id { get; set; }

    public int SourceEventId { get; set; }
    public Event SourceEvent { get; set; } = null!;

    public int TargetEventId { get; set; }
    public Event TargetEvent { get; set; } = null!;

    /// <summary>Тип связи между событиями (например, «Предшествует», «Следует за», «Связано с»).</summary>
    public string RelationshipType { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    [NotMapped]
    public string RelationshipTypeDisplay => string.IsNullOrWhiteSpace(RelationshipType) ? "Связано с" : RelationshipType;
}
