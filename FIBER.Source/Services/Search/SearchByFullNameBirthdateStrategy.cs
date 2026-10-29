using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services.Search;

// Поиск по ФИО (LastName + FirstName + SecondName) И дате рождения — условия объединяются AND.
// Строка поиска может содержать и ФИО, и дату: «Иванов Иван 01.02.2000» или «Иванов 2000».
//
// Логика:
//  • дата (dd.MM.yyyy / yyyy-MM-dd / yyyyMMdd / d.M.yy) или год (4 цифры 1900..2100) распознаются
//    в запросе и исключаются из слов;
//  • каждое оставшееся слово обязано встречаться хотя бы в одном компоненте ФИО (слова — AND);
//  • дата сравнивается точно (по дням), год — по году рождения;
//  • если в запросе не распознано ничего осмысленного — пустая выборка (а не все записи подряд).
//
// Все предикаты строятся через PredicateBuilder и EF.Functions.Like — гарантированно
// транслируются провайдером SQLite в SQL (нет ошибок трансляции и client evaluation).
public sealed class SearchByFullNameBirthdateStrategy : ISearchStrategy
{
    public string Key => "fullname_birthdate";
    public string DisplayName => "ФИО + дата рождения";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        var parsed = SearchTerm.Parse(term);
        if (!parsed.HasAnything) return query; // пустой запрос — без фильтра

        IQueryable<Person> q = query;

        if (parsed.HasName)
        {
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

            q = q.Where(predicate);
        }

        if (parsed.FullDate != null)
        {
            // Точное совпадение даты рождения (сравнение по дням, время игнорируется).
            var start = parsed.FullDate.Value;
            var end = start.AddDays(1);
            q = q.Where(p => p.BirthDate != null && p.BirthDate >= start && p.BirthDate < end);
        }
        else if (parsed.Year != null)
        {
            var y = parsed.Year.Value;
            q = q.Where(p => p.BirthDate != null && p.BirthDate.Value.Year == y);
        }

        return q;
    }
}
