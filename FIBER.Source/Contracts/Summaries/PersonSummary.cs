using System;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Summaries;

public record PersonSummary(
    int Id,
    string DisplayName,
    IdentityStatus Status,
    DateTime? BirthDate,
    int MediaCount = 0,
    int AliasCount = 0)
{
    public string StatusDisplay => Status >= IdentityStatus.Identified
        ? "Identified"
        : "Not identified";

    public string AliasCountDisplay => AliasCount > 0 ? $" (+{AliasCount})" : string.Empty;

    public string BirthDateDisplay => BirthDate?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";
}