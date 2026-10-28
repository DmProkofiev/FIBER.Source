using System.Collections.Generic;

namespace FIBER.Source.Interfaces;

// Фабрика стратегий сравнения Person по ключу (имени)
public interface IComparisonStrategyFactory
{
    // Возвращает стратегию по ключу (или default)
    IComparisonStrategy Resolve(string? key);

    // Все стратегии для UI
    IReadOnlyList<IComparisonStrategy> All { get; }
}