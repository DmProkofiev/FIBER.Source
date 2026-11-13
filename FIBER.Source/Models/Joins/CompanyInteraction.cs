using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Models.Joins;

public class CompanyInteraction
{
    public int Id { get; set; }

    public int SourceCompanyId { get; set; }
    public Company SourceCompany { get; set; } = null!;

    public int TargetCompanyId { get; set; }
    public Company TargetCompany { get; set; } = null!;

    /// <summary>Тип взаимодействия исходной организации по отношению к целевой.</summary>
    public string InteractionType { get; set; } = string.Empty;

    /// <summary>Тип взаимодействия целевой организации по отношению к исходной.</summary>
    public string InteractionTypeTo { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string InteractionTypeDisplay => string.IsNullOrWhiteSpace(InteractionType) ? "Взаимодействие" : InteractionType;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string InteractionTypeToDisplay => string.IsNullOrWhiteSpace(InteractionTypeTo) ? "Взаимодействие" : InteractionTypeTo;
}
