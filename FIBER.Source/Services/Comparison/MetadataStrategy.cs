using System;
using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Comparison;

// Взвешенное сравнение по метаданным (ТЗ §5.4.3).
// Поле участвует только если у обеих сторон непустое значение;
// score = Σ(вес × совпадение) / Σ(вес). Без штрафов за малое число полей.
public sealed class MetadataStrategy : IComparisonStrategy
{
    public string Name => "Сравнение по метаданным";

    public double Compare(Person a, Person b)
    {
        if (a is null || b is null) return 0.0;

        double comparableWeight = 0.0;
        double matchedWeight = 0.0;

        void Add(double weight, Func<string?, string?> normalize, string? x, string? y)
        {
            if (string.IsNullOrWhiteSpace(x) || string.IsNullOrWhiteSpace(y)) return;

            comparableWeight += weight;
            if (string.Equals(normalize(x), normalize(y), StringComparison.OrdinalIgnoreCase))
                matchedWeight += weight;
        }

        Add(5.0, Trimmed, a.Passport, b.Passport);
        Add(3.0, DigitsOnly, a.Phone, b.Phone);
        Add(3.0, Trimmed, a.Mail, b.Mail);
        Add(1.5, Trimmed, a.Address, b.Address);
        Add(1.5, Trimmed, a.SocialMedia, b.SocialMedia);
        Add(1.0, Trimmed, a.Car, b.Car);
        Add(0.5, Trimmed, a.City, b.City);
        Add(0.5, Trimmed, a.Job, b.Job);
        Add(0.3, Trimmed, a.CountryCode, b.CountryCode);

        if (comparableWeight <= 0) return 0.0;

        return Math.Clamp(matchedWeight / comparableWeight, 0.0, 1.0);
    }

    // Trim + нижний регистр (сравнение регистронезависимо).
    private static string Trimmed(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    // Телефон сравнивается только по цифрам.
    private static string DigitsOnly(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
}
