using FIBER.Source.Models;

namespace FIBER.Source.Infrastructure.Data.Configurations;

internal static class CountrySeedData
{
    public static readonly Country[] All =
    {
        new() { Code = "RU", NameRu = "Россия", NameEn = "Russia", IsActive = true },
        new() { Code = "BY", NameRu = "Беларусь", NameEn = "Belarus", IsActive = true },
        new() { Code = "KZ", NameRu = "Казахстан", NameEn = "Kazakhstan", IsActive = true },
        new() { Code = "UA", NameRu = "Украина", NameEn = "Ukraine", IsActive = true },
        new() { Code = "AM", NameRu = "Армения", NameEn = "Armenia", IsActive = true },
        new() { Code = "GE", NameRu = "Грузия", NameEn = "Georgia", IsActive = true },
        new() { Code = "UZ", NameRu = "Узбекистан", NameEn = "Uzbekistan", IsActive = true },
        new() { Code = "KG", NameRu = "Кыргызстан", NameEn = "Kyrgyzstan", IsActive = true },
        new() { Code = "TJ", NameRu = "Таджикистан", NameEn = "Tajikistan", IsActive = true },
        new() { Code = "MD", NameRu = "Молдова", NameEn = "Moldova", IsActive = true },
        new() { Code = "AZ", NameRu = "Азербайджан", NameEn = "Azerbaijan", IsActive = true },
        new() { Code = "LV", NameRu = "Латвия", NameEn = "Latvia", IsActive = true },
        new() { Code = "LT", NameRu = "Литва", NameEn = "Lithuania", IsActive = true },
        new() { Code = "EE", NameRu = "Эстония", NameEn = "Estonia", IsActive = true },
        new() { Code = "DE", NameRu = "Германия", NameEn = "Germany", IsActive = true },
        new() { Code = "FR", NameRu = "Франция", NameEn = "France", IsActive = true },
        new() { Code = "IT", NameRu = "Италия", NameEn = "Italy", IsActive = true },
        new() { Code = "ES", NameRu = "Испания", NameEn = "Spain", IsActive = true },
        new() { Code = "GB", NameRu = "Великобритания", NameEn = "United Kingdom", IsActive = true },
        new() { Code = "US", NameRu = "США", NameEn = "United States", IsActive = true },
        new() { Code = "CA", NameRu = "Канада", NameEn = "Canada", IsActive = true },
        new() { Code = "CN", NameRu = "Китай", NameEn = "China", IsActive = true },
        new() { Code = "JP", NameRu = "Япония", NameEn = "Japan", IsActive = true },
        new() { Code = "KR", NameRu = "Южная Корея", NameEn = "South Korea", IsActive = true },
        new() { Code = "IN", NameRu = "Индия", NameEn = "India", IsActive = true },
        new() { Code = "TR", NameRu = "Турция", NameEn = "Turkey", IsActive = true },
        new() { Code = "IL", NameRu = "Израиль", NameEn = "Israel", IsActive = true },
        new() { Code = "AE", NameRu = "ОАЭ", NameEn = "United Arab Emirates", IsActive = true },
        new() { Code = "BR", NameRu = "Бразилия", NameEn = "Brazil", IsActive = true },
        new() { Code = "AR", NameRu = "Аргентина", NameEn = "Argentina", IsActive = true },
        new() { Code = "AU", NameRu = "Австралия", NameEn = "Australia", IsActive = true },
    };
}