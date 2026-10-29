using FIBER.Source.Models.Enums;

namespace FIBER.Source.ViewModels;

// Одна связь для отображения в SearchView: тип связи и целевой объект.
// Поля TargetType / TargetId позволяют выполнить переход на связанный объект.
// Только для просмотра — CRUD выполняется в режиме редактирования записи.
public sealed class RelationItemViewModel
{
    public RelationItemViewModel(string kind, string target)
    {
        Kind = kind;
        Target = target;
    }

    public RelationItemViewModel(string kind, string target, RelationTargetType targetType, int targetId)
        : this(kind, target)
    {
        TargetType = targetType;
        TargetId = targetId;
    }

    public string Kind { get; }
    public string Target { get; }

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
