using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Comparison;

public class LevenshteinStrategy : IComparisonStrategy
{
    public string Name => "Нечеткое сравнение (Левенштейн)";

    public double Compare(Person a, Person b)
    {
        if (string.IsNullOrEmpty(a.FullName) || string.IsNullOrEmpty(b.FullName)) return 0;

        int distance = CalculateLevenshtein(a.FullName.ToLower(), b.FullName.ToLower());
        int maxLen = Math.Max(a.FullName.Length, b.FullName.Length);

        return 1.0 - ((double)distance / maxLen);
    }

    private int CalculateLevenshtein(string s, string t)
    {
        int[,] d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;
        for (int i = 1; i <= s.Length; i++)
            for (int j = 1; j <= t.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));
        return d[s.Length, t.Length];
    }
}