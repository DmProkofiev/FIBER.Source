namespace FIBER.Source.Infrastructure.Helpers;

/// <summary>
/// Общие строковые утилиты, используемые несколькими сервисами.
/// Устраняет дублирование приведения значений к null при пустой строке.
/// </summary>
public static class StringHelpers
{
    /// <summary>
    /// Приводит пустую или состоящую из пробелов строку к null, иначе возвращает обрезанное значение.
    /// </summary>
    public static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Проверяет, что строка заполнена (не null, не пустая, не только пробелы).
    /// </summary>
    public static bool HasValue(string? value) =>
        !string.IsNullOrWhiteSpace(value);
}
