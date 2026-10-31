using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Interfaces;

public sealed record DuplicateCandidate(Person Person, double Score)
{
    public int Id => Person.Id;
    public string DisplayName => Person.FullName
        ?? Person.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
        ?? $"Аноним #{Person.Id}";
    public string BirthDate => Person.BirthDate?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";
    public string GenderText => Person.Gender switch
    {
        Gender.Male => "Мужской",
        Gender.Female => "Женский",
        _ => "Не указан"
    };
    public string ScoreText => $"{Score * 100:0}%";
}

// Кандидат в дубликаты организации.
public sealed record CompanyDuplicateCandidate(Company Company, double Score)
{
    public int Id => Company.Id;
    public string DisplayName => Company.Name;
    public string RegistrationNumber =>
        string.IsNullOrWhiteSpace(Company.RegistrationNumber) ? "Сведения отсутствуют" : Company.RegistrationNumber!;
    public string ScoreText => $"{Score * 100:0}%";
}

public sealed record DuplicateFinderResult(
    Person Source,
    IReadOnlyList<DuplicateCandidate> Candidates);

public sealed record CompanyDuplicateFinderResult(
    Company Source,
    IReadOnlyList<CompanyDuplicateCandidate> Candidates);

public interface IDuplicateFinderService
{
    IReadOnlyList<IComparisonStrategy> Strategies { get; }

    Task<DuplicateFinderResult?> FindAsync(
        int sourcePersonId,
        string? strategyKey,
        double minScore = 0.0,
        CancellationToken ct = default);

    // Поиск дубликатов организации по ключевым полям (название / рег. номер).
    Task<CompanyDuplicateFinderResult?> FindCompanyAsync(
        int sourceCompanyId,
        string strategyKey,
        double minScore = 0.0,
        CancellationToken ct = default);
}