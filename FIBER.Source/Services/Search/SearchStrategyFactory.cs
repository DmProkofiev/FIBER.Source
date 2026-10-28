using System;
using System.Collections.Generic;
using System.Linq;
using FIBER.Source.Interfaces;

namespace FIBER.Source.Services.Search;

// Разрешает стратегию по ключу; default — fullname
public sealed class SearchStrategyFactory : ISearchStrategyFactory
{
    private readonly Dictionary<string, ISearchStrategy> _byKey;
    private readonly ISearchStrategy _default;

    // Сохраняет стратегии и определяет default
    public SearchStrategyFactory(IEnumerable<ISearchStrategy> strategies)
    {
        var list = strategies.ToList();
        _byKey = list.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        _default = _byKey.TryGetValue("fullname", out var d) ? d : list.First();
    }

    public IReadOnlyList<ISearchStrategy> All => _byKey.Values.ToList();

    // Возвращает стратегию по ключу; null/неизвестный — default
    public ISearchStrategy Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return _default;
        return _byKey.TryGetValue(key, out var s) ? s : _default;
    }
}