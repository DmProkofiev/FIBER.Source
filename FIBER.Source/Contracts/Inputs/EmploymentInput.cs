using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Contracts.Inputs
{
    public record EmploymentInput(
        int PersonId,
        int CompanyId,
        string Position,
        DateTime? StartDate,
        DateTime? EndDate);
}
