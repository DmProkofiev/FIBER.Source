using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FIBER.Source.Interfaces;

namespace FIBER.Source.Services;

public sealed class DuplicateFinderService : IDuplicateFinderService
{
    private readonly IPersonService _personService;
    private readonly IComparisonStrategyFactory _strategyFactory;

    public DuplicateFinderService(
        IPersonService personService,
        IComparisonStrategyFactory strategyFactory)
    {
        _personService = personService;
        _strategyFactory = strategyFactory;
    }

    public IReadOnlyList<IComparisonStrategy> Strategies => _strategyFactory.All;

    public async Task<DuplicateFinderResult?> FindAsync(
        int sourcePersonId,
        string? strategyKey,
        double minScore = 0.0,
        CancellationToken ct = default)
    {
        var source = await _personService.GetByIdAsync(sourcePersonId);
        if (source is null) return null;

        var strategy = _strategyFactory.Resolve(strategyKey);
        var all = await _personService.GetAllExceptAsync(sourcePersonId);

        var candidates = new List<DuplicateCandidate>(all.Count);
        foreach (var candidate in all)
        {
            if (ct.IsCancellationRequested) break;

            double score;
            try { score = strategy.Compare(source, candidate); }
            catch { score = 0; }

            if (score > 0 && score >= minScore)
                candidates.Add(new DuplicateCandidate(candidate, score));
        }

        var ordered = candidates.OrderByDescending(c => c.Score).ToList();
        return new DuplicateFinderResult(source, ordered);
    }
}