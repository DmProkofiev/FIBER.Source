using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

// Редактируемая строка связи «персона ↔ персона» в форме редактирования личности.
public partial class PersonRelationshipEditVM : ObservableObject
{
    // Id существующей записи PersonRelationship; null — новая связь.
    public int? Id { get; set; }

    [ObservableProperty] private int? _relatedPersonId;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private PersonRelationKind _kind = PersonRelationKind.Friend;
    [ObservableProperty] private string _notes = string.Empty;

    // Только для отображения: связь пришла от другой персоны (обратная сторона).
    // Такие строки не отправляются в input.Relationships (их зеркало управляется со стороны инициатора).
    public bool IsReverseOnly { get; set; }

    public static IReadOnlyList<PersonRelationKindOption> AvailableKinds => PersonRelationKinds.AllOptions;

    public PersonRelationshipEditInput ToInput() => new(Id, RelatedPersonId!.Value, Kind, Notes);

    public static PersonRelationshipEditVM FromModel(PersonRelationship r) => new()
    {
        Id = r.Id,
        RelatedPersonId = r.RelatedPersonId,
        DisplayName = r.RelatedPerson?.FullName
            ?? r.RelatedPerson?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
            ?? $"Аноним #{r.RelatedPersonId}",
        Kind = r.Kind,
        Notes = r.Notes,
        IsReverseOnly = false
    };

    public static PersonRelationshipEditVM FromReverse(PersonRelationship r) => new()
    {
        // Id не пробрасываем: обратная связь редактируется только со стороны инициатора.
        Id = null,
        RelatedPersonId = r.PersonId,
        DisplayName = r.Person?.FullName
            ?? r.Person?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
            ?? $"Аноним #{r.PersonId}",
        Kind = r.Kind,
        Notes = r.Notes,
        IsReverseOnly = true
    };
}
