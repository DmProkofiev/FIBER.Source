using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Contracts.Inputs
{
    public record CompanyInteractionInput(
        int SourceCompanyId,
        int TargetCompanyId,
        string InteractionType,
        string Notes);
}
