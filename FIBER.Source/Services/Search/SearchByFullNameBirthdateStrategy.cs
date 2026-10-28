using System;
using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

// Поиск по ФИО ИЛИ дате рождения
public sealed class SearchByFullNameBirthdateStrategy : ISearchStrategy
{
    public string Key => "fullname_birthdate";
    public string DisplayName => "ФИО дата рождения";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var t = term.Trim();

        // Год: 1900..2100
        if (t.Length == 4 && int.TryParse(t, out var year) && year >= 1900 && year <= 2100)
        {
            return query.Where(p =>
                (p.FullName != null && p.FullName.Contains(t)) ||
                (p.BirthDate != null && p.BirthDate.Value.Year == year));
        }

        // Полная дата
        if (DateTime.TryParse(t, out var date))
        {
            var dateOnly = date.Date;
            return query.Where(p =>
                (p.FullName != null && p.FullName.Contains(t)) ||
                (p.BirthDate != null && p.BirthDate.Value.Date == dateOnly));
        }

        // Только ФИО
        return query.Where(p => p.FullName != null && p.FullName.Contains(t));
    }
}