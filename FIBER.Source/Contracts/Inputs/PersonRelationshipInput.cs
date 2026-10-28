using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Contracts.Inputs
{
    public record PersonRelationshipInput(
        int PersonId,
        int RelatedPersonId,
        string RelationType,
        string Notes);
}
