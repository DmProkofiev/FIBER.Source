using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

// Редактируемая строка связи «событие ↔ событие» в форме редактирования события.
public partial class EventEventEditVM : ObservableObject
{
    // Id существующей записи EventEvent; null — новая связь.
    public int? Id { get; set; }

    [ObservableProperty] private int? _sourceEventId;
    [ObservableProperty] private string _sourceEventTitle = string.Empty;
    [ObservableProperty] private int? _targetEventId;
    [ObservableProperty] private string _targetEventTitle = string.Empty;
    [ObservableProperty] private string _relationshipType = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public EventEventEditInput ToInput() => new(Id, SourceEventId.GetValueOrDefault(), TargetEventId.GetValueOrDefault(), RelationshipType, Notes);

    public static EventEventEditVM FromModel(EventEvent ee) => new()
    {
        Id = ee.Id,
        SourceEventId = ee.SourceEventId,
        SourceEventTitle = ee.SourceEvent?.Title ?? $"Событие #{ee.SourceEventId}",
        TargetEventId = ee.TargetEventId,
        TargetEventTitle = ee.TargetEvent?.Title ?? $"Событие #{ee.TargetEventId}",
        RelationshipType = ee.RelationshipType,
        Notes = ee.Notes
    };
}
