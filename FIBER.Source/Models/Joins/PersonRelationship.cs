using System.ComponentModel.DataAnnotations.Schema;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;

namespace FIBER.Source.Models.Joins;

public class PersonRelationship
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public int RelatedPersonId { get; set; }
    public PersonRelationKind Kind { get; set; }
    public string Notes { get; set; } = string.Empty;

    public Person Person { get; set; } = null!;
    public Person RelatedPerson { get; set; } = null!;

    [NotMapped]
    public string KindDisplay => PersonRelationKinds.DisplayNameForRelated(
        Kind, RelatedPerson?.Gender ?? Gender.Unknown);
}