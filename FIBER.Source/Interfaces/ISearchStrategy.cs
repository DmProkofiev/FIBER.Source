using System.Linq;
using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

// Стратегия поиска по Person: применяет фильтр к IQueryable
public interface ISearchStrategy
{
    string Key { get; }
    string DisplayName { get; }
    IQueryable<Person> Apply(IQueryable<Person> query, string? term);
}