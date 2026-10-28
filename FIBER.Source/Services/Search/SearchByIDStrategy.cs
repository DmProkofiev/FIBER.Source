using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

// Поиск по идентификатору
public sealed class SearchByIDStrategy : ISearchStrategy
{
    public string Key => "id";
    public string DisplayName => "По ID";
    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        if (!int.TryParse(term.Trim(), out var id))
            return query.Where(_ => false);
        return query.Where(p => p.Id == id);
    }
}