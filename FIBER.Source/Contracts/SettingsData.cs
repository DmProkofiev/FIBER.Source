using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts;

public sealed record SettingsMedia(MediaItemDto Media, MediaOwnerType OwnerType, int OwnerId, string OwnerName);

public sealed record SettingsData(
    IReadOnlyList<PersonSummary> People,
    IReadOnlyList<CompanySummary> Companies,
    IReadOnlyList<EventSummary> Events,
    IReadOnlyList<AuditLogEntry> Logs,
    IReadOnlyList<SettingsMedia> Media);
