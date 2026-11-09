using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services.Search;

// Поиск по ФИО (LastName + FirstName + SecondName), без учёта регистра.

public sealed class SearchByFullNameStrategy : ISearchStrategy
{
    public string Key => "fullname";
    public string DisplayName => "По ФИО";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        var parsed = SearchTerm.Parse(term);
        if (!parsed.HasAnything) return query;
        if (!parsed.HasName) return query.Where(_ => false);

        var predicate = PredicateBuilder.Empty<Person>();
        foreach (var word in parsed.Words)
        {
            var wordPredicate = PredicateBuilder.False<Person>();
            foreach (var v in SqliteLike.CaseVariants(word))
            {
                var escaped = SqliteLike.Escape(v);
                var localV = escaped;
                wordPredicate = wordPredicate.Or(p => EF.Functions.Like(p.LastName!, "%" + localV + "%"));
                wordPredicate = wordPredicate.Or(p => EF.Functions.Like(p.FirstName!, "%" + localV + "%"));
                wordPredicate = wordPredicate.Or(p => EF.Functions.Like(p.SecondName!, "%" + localV + "%"));
            }

            predicate = predicate.And(wordPredicate);
        }

        return query.Where(predicate);
    }
}
