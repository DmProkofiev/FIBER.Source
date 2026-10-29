using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services.Search;

// Поиск по подстроке псевдонима (без учёта регистра, включая кириллицу);
// применим и к анонимам.
public sealed class SearchByAliasStrategy : ISearchStrategy
{
    public string Key => "alias";
    public string DisplayName => "По псевдониму";

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
            predicate = predicate.Or(
                p => p.Aliases.Any(a => EF.Functions.Like(a.Value, "%" + localV + "%")));
        }

        return query.Where(predicate);
    }
}
