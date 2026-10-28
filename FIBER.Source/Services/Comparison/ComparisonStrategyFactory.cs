using System;
using System.Collections.Generic;
using System.Linq;
using FIBER.Source.Interfaces;

namespace FIBER.Source.Services.Comparison;

// Разрешает стратегию сравнения по имени
public sealed class ComparisonStrategyFactory : IComparisonStrategyFactory
{
    private readonly Dictionary<string, IComparisonStrategy> _byKey;
    private readonly IComparisonStrategy _default;

    // Сохраняет стратегии по имени
    public ComparisonStrategyFactory(IEnumerable<IComparisonStrategy> strategies)
    {
        var list = strategies.ToList();
        _byKey = list.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        _default = list.First();
    }

    public IReadOnlyList<IComparisonStrategy> All => _byKey.Values.ToList();

    // Возвращает стратегию по ключу (имени); null/неизвестный — default
    public IComparisonStrategy Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return _default;
        return _byKey.TryGetValue(key, out var s) ? s : _default;
    }
}