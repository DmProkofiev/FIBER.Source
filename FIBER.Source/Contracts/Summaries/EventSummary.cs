using System;

namespace FIBER.Source.Contracts.Summaries;

public record EventSummary(
    int Id,
    string Title,
    DateTime? Date,
    string? OrganizerCompanyName,
    string? OrganizerPersonName,
    int MediaCount = 0,
    int ParticipantCount = 0)
{
    public string DateDisplay => Date?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";

    public string OrganizerDisplay
    {
        get
        {
            var company = string.IsNullOrWhiteSpace(OrganizerCompanyName) ? null : OrganizerCompanyName;
            var person = string.IsNullOrWhiteSpace(OrganizerPersonName) ? null : OrganizerPersonName;

            if (company != null && person != null) return $"{company} ({person})";
            if (company != null) return company;
            if (person != null) return person;
            return "Сведения отсутствуют";
        }
    }
}