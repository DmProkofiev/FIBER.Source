using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Models
{
    public class Country
    {
        public string Code { get; set; } = string.Empty;
        public string NameRu { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
