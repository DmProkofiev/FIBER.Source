using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

public partial class PersonRelationshipEditVM : ObservableObject
{
    public int? Id { get; set; }

    [ObservableProperty] private int? _relatedPersonId;
    [ObservableProperty] private string _displayName = string.Empty;
    /// <summary>Имя исходного объекта связи (редактируемая персоны или инициатор).</summary>
    [ObservableProperty] private string _sourceDisplayName = string.Empty;
    [ObservableProperty] private PersonRelationKind _kindFrom;
    [ObservableProperty] private PersonRelationKind _kindTo;
    [ObservableProperty] private string _notes = string.Empty;

    public bool IsReverseOnly { get; set; }

    public static IReadOnlyList<PersonRelationKindOption> AvailableKinds => PersonRelationKinds.AllOptions;

    public PersonRelationshipEditInput ToInput() => new(Id, RelatedPersonId!.Value, KindFrom, KindTo, Notes);

    public static PersonRelationshipEditVM FromModel(PersonRelationship r) => new()
    {
        Id = r.Id,
        RelatedPersonId = r.RelatedPersonId,
        DisplayName = r.RelatedPerson?.FullName
            ?? r.RelatedPerson?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
            ?? $"Аноним #{r.RelatedPersonId}",
        KindFrom = r.KindFrom,
        KindTo = r.KindTo,
        Notes = r.Notes,
        IsReverseOnly = false
    };

    public static PersonRelationshipEditVM FromReverse(PersonRelationship r) => new()
    {
        Id = r.Id,
        RelatedPersonId = r.PersonId,
        DisplayName = r.Person?.FullName
            ?? r.Person?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
            ?? $"Аноним #{r.PersonId}",
        KindFrom = r.KindTo,
        KindTo = r.KindFrom,
        Notes = r.Notes,
        IsReverseOnly = true
    };
}
