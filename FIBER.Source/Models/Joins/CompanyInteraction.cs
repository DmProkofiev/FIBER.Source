using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Models.Joins
{
    /// <summary>Взаимодействие между организациями (источник → цель).</summary>
    public class CompanyInteraction
    {
        public int Id { get; set; }

        public int SourceCompanyId { get; set; }
        public int TargetCompanyId { get; set; }

        public string InteractionType { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public Company SourceCompany { get; set; } = null!;
        public Company TargetCompany { get; set; } = null!;
    }
}
