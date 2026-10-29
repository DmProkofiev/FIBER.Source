using System;
using System.Linq;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Search;

// Вспомогательные методы для стратегий поиска по Person.
// Сущность Person не изменяется: «ФИО» собирается из LastName, FirstName, SecondName.
public static class PersonSearchFilters
{
    // Приведение строки поиска к единому виду: trim, схлопывание повторных пробелов, нижний регистр.
    public static string Normalize(string? value) => SearchTerm.NormalizeWord(value);

    // Склейка частей ФИО через одиночный пробел, пропуская пустые компоненты (in-memory вариант).
    public static string JoinFullName(string? lastName, string? firstName, string? secondName) =>
        string.Join(" ", new[] { lastName, firstName, secondName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    // Нормализация номера телефона: остаются только цифры («+7 (900) 111-22-33» -> «79001112233»).
    public static string DigitsOnly(string? value) =>
        value == null ? string.Empty : new(value.Where(char.IsDigit).ToArray());
}
