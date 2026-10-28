using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

public sealed class SearchByPhoneStrategy : ISearchStrategy
{
    public string Key => "phone";
    public string DisplayName => "По телефону";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var t = term.Trim();
        return query.Where(p => p.Phone != null && p.Phone.Contains(t));
    }
}