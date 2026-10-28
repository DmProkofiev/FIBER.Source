using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

public sealed record DuplicateCandidate(Person Person, double Score)
{
    public string DisplayName => Person.FullName ?? $"Аноним #{Person.Id}";
    public string BirthDate => Person.BirthDate?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";
    public string StatusText => Person.Status.ToString();
    public string ScoreText => $"{Score * 100:0}%";
}

public sealed record DuplicateFinderResult(
    Person Source,
    IReadOnlyList<DuplicateCandidate> Candidates);

public interface IDuplicateFinderService
{
    Task<DuplicateFinderResult?> FindAsync(
        int sourcePersonId,
        string? strategyKey,
        double minScore = 0.0,
        CancellationToken ct = default);
}