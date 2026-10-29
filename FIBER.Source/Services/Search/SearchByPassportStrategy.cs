using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services.Search;

// Поиск по подстроке паспорта (без учёта регистра, включая кириллицу).
// Используется переводимая для SQLite конструкция EF.Functions.Like (Contains со StringComparison не транслируется).
public sealed class SearchByPassportStrategy : ISearchStrategy
{
    public string Key => "passport";
    public string DisplayName => "По паспорту";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        var t = SearchTerm.NormalizeWord(term);
        if (t.Length == 0) return query;

        // Все варианты регистра ищутся через OR (поддержка кириллицы и латиницы).
        var predicate = PredicateBuilder.False<Person>();
        foreach (var v in SqliteLike.CaseVariants(t))
        {
            var escaped = SqliteLike.Escape(v);
            var localV = escaped;
            predicate = predicate.Or(p => EF.Functions.Like(p.Passport!, "%" + localV + "%"));
        }

        return query.Where(predicate);
    }
}
