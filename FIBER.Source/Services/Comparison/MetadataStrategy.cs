using System;
using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Comparison;

// Сравнение по метаданным: телефон, email, город/страна, транспорт, профессия.
// 100% требует не менее 3 совпавших полей — одно совпадение больше не даёт 1.0.
public sealed class MetadataStrategy : IComparisonStrategy
{
    // Минимум совпавших полей для 100%
    private const int MinFieldsForFullMatch = 3;

    public string Name => "Сравнение по метаданным";

    public double Compare(Person a, Person b)
    {
        if (a is null || b is null || a.Id == b.Id) return 0;

        int comparable = 0;
        int matched = 0;

        void Add(bool equal)
        {
            comparable++;
            if (equal) matched++;
        }

        if (!string.IsNullOrWhiteSpace(a.Phone) && !string.IsNullOrWhiteSpace(b.Phone))
            Add(Normalize(a.Phone) == Normalize(b.Phone));

        if (!string.IsNullOrWhiteSpace(a.Mail) && !string.IsNullOrWhiteSpace(b.Mail))
            Add(string.Equals(a.Mail, b.Mail, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(a.City))
            Add(string.Equals(a.City, b.City, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(a.CountryCode))
            Add(string.Equals(a.CountryCode, b.CountryCode, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(a.Car))
            Add(string.Equals(a.Car, b.Car, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(a.Job))
            Add(string.Equals(a.Job, b.Job, StringComparison.OrdinalIgnoreCase));

        if (comparable == 0) return 0;

        double score = (double)matched / comparable;

        // Штраф: < MinFieldsForFullMatch совпавших → не более matched/MinFields
        if (matched < MinFieldsForFullMatch)
            score *= (double)matched / MinFieldsForFullMatch;

        return Math.Min(1.0, score);
    }

    // Оставляет только цифры в номере телефона
    private static string Normalize(string s) =>
        new string(s.Where(char.IsDigit).ToArray());
}