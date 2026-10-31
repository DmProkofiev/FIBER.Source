using System;
using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Comparison;

// Нечёткое сравнение (ТЗ §5.4.2): расстояние Дамерау–Левенштейна по имени
// с множителем по дате рождения.
public sealed class LevenshteinStrategy : IComparisonStrategy
{
    public string Name => "Нечеткое сравнение (Левенштейн)";

    public double Compare(Person a, Person b)
    {
        if (a is null || b is null) return 0.0;

        var nameA = Normalize(ResolveName(a));
        var nameB = Normalize(ResolveName(b));
        if (nameA.Length == 0 || nameB.Length == 0) return 0.0;

        int distance = CalculateDamerauLevenshtein(nameA, nameB);
        int maxLen = Math.Max(nameA.Length, nameB.Length);
        double nameScore = 1.0 - ((double)distance / maxLen);

        // score = nameScore * factor(BirthDate), но не ниже 0.
        double score = nameScore * BirthDateFactor(a.BirthDate, b.BirthDate);
        return Math.Clamp(score, 0.0, 1.0);
    }

    // Идентифицированный — ФИО; аноним — основной псевдоним.
    private static string? ResolveName(Person person) =>
        person.FullName ?? person.Aliases.FirstOrDefault(x => x.IsPrimary)?.Value;

    // Нормализация: trim, нижний регистр, сворачивание повторных пробелов.
    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();

    // Множитель по дате рождения (ТЗ §5.4.2).
    private static double BirthDateFactor(DateTime? a, DateTime? b)
    {
        if (!a.HasValue && !b.HasValue) return 0.6;
        if (!a.HasValue || !b.HasValue) return 0.7;

        int days = Math.Abs((a.Value.Date - b.Value.Date).Days);
        if (days == 0) return 1.0;
        if (days <= 365) return 0.9;
        return 0.3;
    }

    // Оптимальное строковое выравнивание (Damerau–Levenshtein, транспозиции соседних символов).
    private static int CalculateDamerauLevenshtein(string s, string t)
    {
        int[,] d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);

                // Транспозиция двух соседних символов.
                if (i > 1 && j > 1 &&
                    s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[s.Length, t.Length];
    }
}
