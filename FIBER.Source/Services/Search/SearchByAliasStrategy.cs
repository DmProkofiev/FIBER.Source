using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

// Поиск по подстроке алиаса; применим и к анонимам (FullName == null)
public sealed class SearchByAliasStrategy : ISearchStrategy
{
    public string Key => "alias";
    public string DisplayName => "По псевдониму";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var t = term.Trim();
        return query.Where(p => p.Aliases.Any(a => a.Value.Contains(t)));
    }
}