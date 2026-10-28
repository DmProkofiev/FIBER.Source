using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

// Поиск по подстроке ФИО
public sealed class SearchByFullNameStrategy : ISearchStrategy
{
    public string Key => "fullname";
    public string DisplayName => "По ФИО";
    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var t = term.Trim();
        return query.Where(p => p.FullName != null && p.FullName.Contains(t));
    }
}