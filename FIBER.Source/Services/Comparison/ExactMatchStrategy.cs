using System;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Comparison;

// Точное совпадение только по идентифицирующим полям (0.0 или 1.0).
// Правила (ТЗ §5.4.1):
//  • паспорт совпал (оба непустые) → 1.0; оба непустые и разные → 0.0;
//  • ФИО + дата рождения совпали, все поля с обеих сторон непустые → 1.0;
//  • анонимы (FullName == null) → 0.0;
//  • null == null совпадением не считается.
public sealed class ExactMatchStrategy : IComparisonStrategy
{
    public string Name => "Точное совпадение";

    public double Compare(Person a, Person b)
    {
        if (a is null || b is null) return 0.0;

        bool aHasPassport = !string.IsNullOrWhiteSpace(a.Passport);
        bool bHasPassport = !string.IsNullOrWhiteSpace(b.Passport);

        if (aHasPassport && bHasPassport)
        {
            return string.Equals(a.Passport!.Trim(), b.Passport!.Trim(), StringComparison.OrdinalIgnoreCase)
                ? 1.0
                : 0.0;
        }

        // Анонимов точное совпадение не идентифицирует (FullName у них null).
        if (a.FullName is null || b.FullName is null) return 0.0;

        // Каждое поле должно быть непустым с обеих сторон — null/пустое не считается совпадением.
        if (!HasValue(a.LastName) || !HasValue(b.LastName)) return 0.0;
        if (!HasValue(a.FirstName) || !HasValue(b.FirstName)) return 0.0;
        if (!HasValue(a.SecondName) || !HasValue(b.SecondName)) return 0.0;
        if (!a.BirthDate.HasValue || !b.BirthDate.HasValue) return 0.0;

        bool equal = string.Equals(a.LastName!.Trim(), b.LastName!.Trim(), StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.FirstName!.Trim(), b.FirstName!.Trim(), StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.SecondName!.Trim(), b.SecondName!.Trim(), StringComparison.OrdinalIgnoreCase)
                     && a.BirthDate.Value.Date == b.BirthDate.Value.Date;

        return equal ? 1.0 : 0.0;
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
