using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FIBER.Source.Services.Search;

// Вспомогательные методы для регистронезависимого LIKE в SQLite с поддержкой кириллицы.
// SQLite NOCASE работает только для ASCII, поэтому ищем по всем вариантам регистра терма
// через OR: x LIKE '%t%' OR x LIKE '%lower(t)%' OR x LIKE '%upper(t)%' OR x LIKE '%Title(t)%'.
// Все возвращаемые значения — простые строки, которые стратегии подставляют
// в EF.Functions.Like прямо внутри лямбда-выражений (такие выражения транслируются в SQL).
public static class SqliteLike
{
    // Экранирование значения для использования внутри шаблона LIKE.
    public static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    // Première буква заглавная, остальные строчные.
    public static string ToTitleCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return char.ToUpperInvariant(value[0]) + value.AsSpan(1).ToString().ToLowerInvariant();
    }

    // Все варианты регистра терма (без дублей).
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
