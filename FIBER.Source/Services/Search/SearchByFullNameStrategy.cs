using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services.Search;

// Поиск по ФИО (LastName + FirstName + SecondName), без учёта регистра.
//
// Почему раньше не было результатов:
// 1) Терм целиком («Иванов Иван») искался как подстрока внутри каждого поля отдельно —
//    в базе «Иванов» и «Иван» лежат в разных колонках, совпадений не находилось никогда.
// 2) LOWER() в SQLite работает только для ASCII, кириллица не приводилась к нижнему регистру.
//
// Теперь запрос собирается динамически (PredicateBuilder): каждое слово запроса должно
// присутствовать хотя бы в одном из трёх компонентов ФИО (условия слов объединяются AND).
// Регистронезависимость для кириллицы и латиницы обеспечивается перебором всех вариантов
// регистра терма через EF.Functions.Like (все конструкции транслируются в SQL).
public sealed class SearchByFullNameStrategy : ISearchStrategy
{
    public string Key => "fullname";
    public string DisplayName => "По ФИО";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        var parsed = SearchTerm.Parse(term);
        if (!parsed.HasAnything) return query; // пустой запрос — без фильтра
        if (!parsed.HasName) return query.Where(_ => false); // одна дата — не поиск по ФИО

        var predicate = PredicateBuilder.Empty<Person>();
        foreach (var word in parsed.Words)
        {
            // Каждое слово должно присутствовать хотя бы в одном компоненте ФИО.
            // Все варианты регистра ищутся через OR (поддержка кириллицы и латиницы).
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
