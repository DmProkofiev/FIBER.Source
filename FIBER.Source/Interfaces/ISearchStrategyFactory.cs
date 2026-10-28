using System.Collections.Generic;

namespace FIBER.Source.Interfaces;

// Фабрика стратегий поиска по ключу
public interface ISearchStrategyFactory
{
    // Возвращает стратегию по ключу (или default при неизвестном ключе)
    ISearchStrategy Resolve(string? key);

    // Список всех стратегий (для UI выбора)
    IReadOnlyList<ISearchStrategy> All { get; }
}