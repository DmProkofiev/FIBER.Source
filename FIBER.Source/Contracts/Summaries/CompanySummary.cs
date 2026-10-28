using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Contracts.Summaries
{
    public record CompanySummary(
        int Id,
        string Name,
        string RegistrationNumber,
        int MediaCount = 0);
}
