using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

// Поиск по идентификатору (точное совпадение Id).
// Пустая строка — без фильтра; нечисловая строка — заведомо пустая выборка.
public sealed class SearchByIDStrategy : ISearchStrategy
{
    public string Key => "id";
    public string DisplayName => "По ID";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        var raw = (term ?? string.Empty).Trim();
        if (raw.Length == 0) return query;
        if (!int.TryParse(raw, out var id))
            return query.Where(_ => false);
        return query.Where(p => p.Id == id);
    }
}
