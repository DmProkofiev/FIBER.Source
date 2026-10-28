using FIBER.Source.Models.Enums;

namespace FIBER.Source.Models.Helpers;

// Опция ComboBox «тип связи»: отображаемое имя всегда относительно связанной персоны.
public sealed class RelationKindOption
{
    public RelationKindOption(PersonRelationKind value, Gender relatedGender)
    {
        Value = value;
        Display = PersonRelationKinds.DisplayNameForRelated(value, relatedGender);
    }

    public PersonRelationKind Value { get; }

    public string Display { get; }
}
