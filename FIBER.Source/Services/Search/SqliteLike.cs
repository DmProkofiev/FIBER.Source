using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FIBER.Source.Services.Search;

public static class SqliteLike
{
    public static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    public static string ToTitleCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return char.ToUpperInvariant(value[0]) + value.AsSpan(1).ToString().ToLowerInvariant();
    }

    public static IEnumerable<string> CaseVariants(string term)
    {
        return new[]
        {
            term,
            term.ToLowerInvariant(),
            term.ToUpperInvariant(),
            ToTitleCase(term)
        }.Distinct();
    }
}
