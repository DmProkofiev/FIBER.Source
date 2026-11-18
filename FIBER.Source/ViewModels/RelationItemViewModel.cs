using FIBER.Source.Models.Enums;

namespace FIBER.Source.ViewModels;

// Одна связь для отображения в SearchView.
// Табличная форма: "Имя (Роль) <-> Имя (Роль)" с отдельными ссылками на навигацию
public sealed class RelationItemViewModel
{
    public RelationItemViewModel(
        string sourceDisplayName, string? sourceRole,
        string targetDisplayName, string? targetRole,
        string? notes,
        RelationTargetType? targetType = null, int? targetId = null,
        RelationTargetType? sourceType = null, int? sourceId = null,
        bool isEventParticipant = false)
    {
        SourceDisplayName = sourceDisplayName;
        SourceRole = sourceRole;
        TargetDisplayName = targetDisplayName;
        TargetRole = targetRole;
        Notes = notes?.Trim() ?? string.Empty;
        TargetType = targetType;
        TargetId = targetId;
        SourceType = sourceType;
        SourceId = sourceId;
        IsEventParticipant = isEventParticipant;
    }

    public string SourceDisplayName { get; }
    public string? SourceRole { get; }
    public string TargetDisplayName { get; }
    public string? TargetRole { get; }
    public string Notes { get; }

    public string SourceDisplayWithRole =>
        HasSourceRole ? $"{SourceDisplayName} ({SourceRole})" : SourceDisplayName;
    public string TargetDisplayWithRole =>
        HasTargetRole ? $"{TargetDisplayName} ({TargetRole})" : TargetDisplayName;
    public bool HasSourceRole => !string.IsNullOrWhiteSpace(SourceRole) && SourceRole != "Другое";
    public bool HasTargetRole => !string.IsNullOrWhiteSpace(TargetRole) && TargetRole != "Другое";
    public bool HasNotes => Notes.Length > 0;
    public RelationTargetType? TargetType { get; }
    public int? TargetId { get; }
    public RelationTargetType? SourceType { get; }
    public int? SourceId { get; }
    public bool CanNavigateTarget => TargetType.HasValue && TargetId.HasValue;
    public bool CanNavigateSource => SourceType.HasValue && SourceId.HasValue;
    public bool IsEventParticipant { get; }
}

// Тип целевого объекта связи.
public enum RelationTargetType
{
    Person,
    Company,
    Event
}
