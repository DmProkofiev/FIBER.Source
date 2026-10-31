using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services;

public sealed class DuplicateFinderService : IDuplicateFinderService
{
    // Стратегии поиска дубликатов организаций (ключ -> отображаемое имя).
    public const string CompanyExactNameKey = "company_exact_name";
    public const string CompanyFuzzyNameKey = "company_fuzzy_name";
    public const string CompanyRegistrationKey = "company_registration";

    private static readonly (string Key, string Display)[] CompanyStrategyOptions =
    {
        (CompanyExactNameKey, "Точное совпадение названия"),
        (CompanyFuzzyNameKey, "Нечёткое сравнение названий (Левенштейн)"),
        (CompanyRegistrationKey, "Совпадение регистрационного номера")
    };

    // Элемент справочника стратегий для ComboBox в XAML.
    public sealed record CompanyStrategyOption(string Key, string Display);

    private readonly IPersonService _personService;
    private readonly ICompanyService _companyService;
    private readonly IComparisonStrategyFactory _strategyFactory;

    public DuplicateFinderService(
        IPersonService personService,
        ICompanyService companyService,
        IComparisonStrategyFactory strategyFactory)
    {
        _personService = personService;
        _companyService = companyService;
        _strategyFactory = strategyFactory;
    }

    public IReadOnlyList<IComparisonStrategy> Strategies => _strategyFactory.All;

    public static IReadOnlyList<CompanyStrategyOption> CompanyStrategies =>
        CompanyStrategyOptions.Select(o => new CompanyStrategyOption(o.Key, o.Display)).ToList();

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

        // ТЗ §5.4.2: для анонимов с коротким (< 5 символов) основным псевдонимом порог 0.75.
        double effectiveMinScore = EffectiveMinScore(source, minScore);

        var candidates = new List<DuplicateCandidate>(all.Count);
        foreach (var candidate in all)
        {
            if (ct.IsCancellationRequested) break;

            double score;
            try { score = strategy.Compare(source, candidate); }
            catch { score = 0; }

            if (score > 0 && score >= effectiveMinScore)
                candidates.Add(new DuplicateCandidate(candidate, score));
        }

        var ordered = candidates.OrderByDescending(c => c.Score).ToList();
        return new DuplicateFinderResult(source, ordered);
    }

    // Минимальный порог отсечения: для анонимов с коротким (< 5 символов) псевдонимом — 0.75.
    private static double EffectiveMinScore(Person source, double minScore)
    {
        if (source.Status != Models.Enums.IdentityStatus.Anonymous) return minScore;

        var primaryAlias = source.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value;
        bool shortAlias = string.IsNullOrWhiteSpace(primaryAlias) || primaryAlias.Trim().Length < 5;
        return shortAlias ? Math.Max(minScore, 0.75) : minScore;
    }

    public async Task<CompanyDuplicateFinderResult?> FindCompanyAsync(
        int sourceCompanyId,
        string strategyKey,
        double minScore = 0.0,
        CancellationToken ct = default)
    {
        var source = await _companyService.GetByIdAsync(sourceCompanyId);
        if (source is null) return null;

        var all = await _companyService.GetAllExceptAsync(sourceCompanyId);

        var candidates = new List<CompanyDuplicateCandidate>(all.Count);
        foreach (var candidate in all)
        {
            if (ct.IsCancellationRequested) break;

            double score;
            try { score = CompareCompanies(source, candidate, strategyKey); }
            catch { score = 0; }

            if (score > 0 && score >= minScore)
                candidates.Add(new CompanyDuplicateCandidate(candidate, score));
        }

        var ordered = candidates.OrderByDescending(c => c.Score).ToList();
        return new CompanyDuplicateFinderResult(source, ordered);
    }

    // Оценка похожести двух организаций по выбранной стратегии.
    private static double CompareCompanies(Company a, Company b, string strategyKey) => strategyKey switch
    {
        CompanyExactNameKey => NamesEqual(a.Name, b.Name) ? 1.0 : 0.0,
        CompanyFuzzyNameKey => FuzzyNameScore(a.Name, b.Name),
        CompanyRegistrationKey => RegistrationScore(a, b),
        _ => FuzzyNameScore(a.Name, b.Name)
    };

    private static bool NamesEqual(string? x, string? y) =>
        !string.IsNullOrWhiteSpace(x) && !string.IsNullOrWhiteSpace(y) &&
        string.Equals(Normalize(x), Normalize(y), StringComparison.OrdinalIgnoreCase);

    private static double RegistrationScore(Company a, Company b)
    {
        var x = Normalize(a.RegistrationNumber);
        var y = Normalize(b.RegistrationNumber);
        if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y)) return 0.0;
        return string.Equals(x, y, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }

    // Нечёткое сравнение: нормализованная дистанция Левенштейна + бонус за общее юридическое ядро названия.
    private static double FuzzyNameScore(string? x, string? y)
    {
        var a = Normalize(x);
        var b = Normalize(y);
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        int distance = Levenshtein(a.ToLowerInvariant(), b.ToLowerInvariant());
        int maxLen = Math.Max(a.Length, b.Length);
        double similarity = 1.0 - ((double)distance / maxLen);

        // Совпадение без кавычек/юр. формы («ООО Ромашка» ≈ «Ромашка») — считаем почти точным.
        var coreA = StripLegalForm(a);
        var coreB = StripLegalForm(b);
        if (!string.IsNullOrEmpty(coreA) && string.Equals(coreA, coreB, StringComparison.OrdinalIgnoreCase))
            similarity = Math.Max(similarity, 0.95);

        return similarity;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Отсечь организационно-правовую форму для сравнения «ядра» названия.
    private static string StripLegalForm(string name)
    {
        string[] forms =
        {
            "ооо", "пао", "пao", "ао", "оао", "зао", "и п", "ип", "нко", "бф", "оао", "пбо",
            "общество с ограниченной ответственностью",
            "публичное акционерное общество",
            "акционерное общество",
            "индивидуальный предприниматель"
        };

        var lowered = name.ToLowerInvariant();
        foreach (var form in forms)
            lowered = lowered.Replace(form, string.Empty);

        return string.Join(' ',
            lowered.Split(new[] { ' ', '.', ',', '"', '\'', '(', ')' },
                    StringSplitOptions.RemoveEmptyEntries));
    }

    private static int Levenshtein(string s, string t)
    {
        int[,] d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;
        for (int i = 1; i <= s.Length; i++)
            for (int j = 1; j <= t.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));
        return d[s.Length, t.Length];
    }
}