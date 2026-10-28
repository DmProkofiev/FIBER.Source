using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Models.Joins
{
    public class Employment
    {
        public int PersonId { get; set; }
        public int CompanyId { get; set; }

        public string Position { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public Person Person { get; set; } = null!;
        public Company Company { get; set; } = null!;
    }
}
