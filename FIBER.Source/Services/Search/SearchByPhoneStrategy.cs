using System.Linq;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services.Search;

// Поиск по телефону: сравнение по цифрам, чтобы «8-900-111» находило запись «89001112233».
public sealed class SearchByPhoneStrategy : ISearchStrategy
{
    public string Key => "phone";
    public string DisplayName => "По телефону";

    public IQueryable<Person> Apply(IQueryable<Person> query, string? term)
    {
        var digits = PersonSearchFilters.DigitsOnly(term);
        if (digits.Length == 0)
        {
            // В запросе не было цифр — ищем как обычную подстроку без учёта регистра
            // переводимой для SQLite конструкцией EF.Functions.Like.
            var t = SqliteLike.Escape(SearchTerm.NormalizeWord(term));
            if (t.Length == 0) return query;
            return query.Where(p => EF.Functions.Like(p.Phone!, "%" + t + "%"));
        }

        // Replace/Contains(string) транслируются провайдером SQLite корректно.
        // Удаляем все распространённые разделители: «+7 (900) 111-22-33» → «79001112233».
        return query.Where(p =>
            p.Phone != null &&
            p.Phone
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("+", "")
                .Replace(".", "")
                .Contains(digits));
    }
}
