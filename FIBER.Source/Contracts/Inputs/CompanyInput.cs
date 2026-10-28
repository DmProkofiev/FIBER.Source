using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Contracts.Inputs
{
    public record CompanyInput(
        string Name,
        string RegistrationNumber,
        string Address,
        string Phone,
        string Mail,
        string Website,
        string Notes,
        IReadOnlyList<CompanyInteractionEditInput>? Interactions = null);
}
