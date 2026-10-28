using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

// Поиск по подстроке паспорта
public sealed class SearchByPassportStrategy : ISearchStrategy
{
    public string Key => "passport";
    public string DisplayName => "По паспорту";
    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var t = term.Trim();
        return query.Where(p => p.Passport != null && p.Passport.Contains(t));
    }
}