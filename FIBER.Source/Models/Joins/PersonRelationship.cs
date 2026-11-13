using System.ComponentModel.DataAnnotations.Schema;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;

namespace FIBER.Source.Models.Joins;

public class PersonRelationship
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public int RelatedPersonId { get; set; }

    /// <summary>Роль Person по отношению к RelatedPerson.</summary>
    public PersonRelationKind KindFrom { get; set; }

    /// <summary>Роль RelatedPerson по отношению к Person.</summary>
    public PersonRelationKind KindTo { get; set; }

    public string Notes { get; set; } = string.Empty;

    public Person Person { get; set; } = null!;
    public Person RelatedPerson { get; set; } = null!;

    [NotMapped]
    public string KindFromDisplay => PersonRelationKinds.DisplayNameForRelated(KindFrom, RelatedPerson?.Gender ?? Gender.Unknown);

    [NotMapped]
    public string KindToDisplay => PersonRelationKinds.DisplayNameForRelated(KindTo, Person?.Gender ?? Gender.Unknown);
}
