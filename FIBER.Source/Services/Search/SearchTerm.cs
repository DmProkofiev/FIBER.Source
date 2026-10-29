using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FIBER.Source.Services.Search;

// Разобранная строка поиска для стратегий «По ФИО» и «ФИО + дата рождения».
// Токены нормализуются (trim, схлопывание пробелов) и приводятся к нижнему регистру —
// провайдер SQLite транслирует LOWER(...) в SQL, поэтому данные приводятся к нижнему
// регистру на стороне СУБД, а терм — здесь.
public sealed class SearchTerm
{
    // Слова запроса без учёта регистра (даты и годы из них исключены).
    public IReadOnlyList<string> Words { get; }

    // Полная дата рождения, если распознана (dd.MM.yyyy, dd/MM/yyyy, yyyy-MM-dd, yyyyMMdd, d.M.yy).
    public DateTime? FullDate { get; }

    // Год рождения, если в запросе указано только он (4 цифры, 1900..2100).
    public int? Year { get; }

    private SearchTerm(IReadOnlyList<string> words, DateTime? fullDate, int? year)
    {
        Words = words;
        FullDate = fullDate;
        Year = year;
    }

    public static SearchTerm Empty { get; } = new(Array.Empty<string>(), null, null);

    public bool HasName => Words.Count > 0;
    public bool HasDate => FullDate != null || Year != null;
    public bool HasAnything => HasName || HasDate;

    // Нормализация: trim, нижний регистр, схлопывание повторных пробелов.
    public static string NormalizeWord(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        while (v.Contains("  ", StringComparison.Ordinal))
            v = v.Replace("  ", " ");
        return v.ToLowerInvariant();
    }

    private static readonly string[] DateFormats =
    {
        "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "yyyyMMdd",
        "d.M.yyyy", "d.M.yy", "dd.MM.yy"
    };

    public static SearchTerm Parse(string? term)
    {
        var raw = (term ?? string.Empty).Trim();
        if (raw.Length == 0) return Empty;

        var tokens = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

        // 1) Пробуем выделить полную дату (подходящий токен с конца).
        DateTime? fullDate = null;
        int dateIndex = -1;
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (DateTime.TryParseExact(tokens[i], DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt) ||
                DateTime.TryParseExact(tokens[i], DateFormats, CultureInfo.CurrentCulture,
                    DateTimeStyles.None, out dt))
            {
                fullDate = dt.Date;
                dateIndex = i;
                break;
            }
        }

        // 2) Если полной даты нет — пробуем выделить год (4 цифры, 1900..2100).
        int? year = null;
        int yearIndex = -1;
        if (fullDate == null)
        {
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i].Length == 4 && int.TryParse(tokens[i], NumberStyles.None,
                        CultureInfo.InvariantCulture, out var y) && y >= 1900 && y <= 2100)
                {
                    year = y;
                    yearIndex = i;
                    break;
                }
            }
        }

        int removeIndex = dateIndex >= 0 ? dateIndex : yearIndex;
        if (removeIndex >= 0) tokens.RemoveAt(removeIndex);

        var words = tokens.Select(NormalizeWord).Where(w => w.Length > 0).ToList();
        return new SearchTerm(words, fullDate, year);
    }
}
