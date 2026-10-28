using System.Collections.Generic;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Models.Helpers;

public sealed record GenderOption(Gender Value, string Display);

public static class GenderOptions
{
    public static IReadOnlyList<GenderOption> All { get; } = new List<GenderOption>
    {
        new(Gender.Unknown, "Не указан"),
        new(Gender.Male, "Мужской"),
        new(Gender.Female, "Женский")
    };
}