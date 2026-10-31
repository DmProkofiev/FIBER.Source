using FIBER.Source.Models.Enums;

namespace FIBER.Source.ViewModels;

// Одна связь для отображения в SearchView: тип связи, целевой объект и
// дополнительные сведения о связи (роль, заметки и т.п.).
// Поля TargetType / TargetId позволяют выполнить переход на связанный объект.
// Только для просмотра — CRUD выполняется в режиме редактирования записи.
public sealed class RelationItemViewModel
{
    public RelationItemViewModel(string kind, string target, string? details = null)
    {
        Kind = kind;
        Target = target;
        Details = details?.Trim() ?? string.Empty;
    }

    public RelationItemViewModel(string kind, string target, RelationTargetType targetType, int targetId,
        string? details = null)
        : this(kind, target, details)
    {
        TargetType = targetType;
        TargetId = targetId;
    }

    public string Kind { get; }
    public string Target { get; }

    /// <summary>Дополнительные сведения о связи; пустая строка — сведений нет.</summary>
    public string Details { get; }

    /// <summary>Признак наличия дополнительных сведений (управляет видимостью в UI).</summary>
    public bool HasDetails => Details.Length > 0;

    /// <summary>Тип целевого объекта (для перехода по ссылке).</summary>
    public RelationTargetType? TargetType { get; }

    /// <summary>Идентификатор целевого объекта (для перехода по ссылке).</summary>
    public int? TargetId { get; }

    /// <summary>Признак того, что по элементу можно перейти (клик).</summary>
    public bool CanNavigate => TargetType.HasValue && TargetId.HasValue;
}

// Тип целевого объекта связи.
public enum RelationTargetType
{
    Person,
    Company,
    Event
}
