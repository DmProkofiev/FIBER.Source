using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Services;

namespace FIBER.Source.ViewModels;

public partial class DuplicateFinderViewModel : ObservableObject
{
    public const double DefaultThreshold = 0.6;

    private readonly IDuplicateFinderService _duplicateFinderService;
    private readonly int _sourcePersonId;
    private readonly int _sourceCompanyId;

    public DuplicateFinderViewModel(int personId, IDuplicateFinderService duplicateFinderService)
    {
        _sourcePersonId = personId;
        _sourceCompanyId = 0;
        _duplicateFinderService = duplicateFinderService;
        Strategies = new ObservableCollection<IComparisonStrategy>(duplicateFinderService.Strategies);
        _selectedStrategy = Strategies.FirstOrDefault();
        _threshold = DefaultThreshold;
    }

    // Режим поиска дубликатов организаций.
    public DuplicateFinderViewModel(
        int sourceCompanyId,
        bool isCompanyMode,
        IDuplicateFinderService duplicateFinderService)
    {
        _sourceCompanyId = sourceCompanyId;
        _sourcePersonId = 0;
        IsCompanyMode = isCompanyMode;
        _duplicateFinderService = duplicateFinderService;
        Strategies = new ObservableCollection<IComparisonStrategy>(duplicateFinderService.Strategies);
        _threshold = DefaultThreshold;
    }

    public bool IsCompanyMode { get; }

    public ObservableCollection<IComparisonStrategy> Strategies { get; }
    public ObservableCollection<DuplicateCandidate> Candidates { get; } = new();
    // Кандидаты в режиме организаций (отдельный список для GridView в XAML).
    public ObservableCollection<CompanyDuplicateCandidate> CompanyCandidates { get; } = new();

    [ObservableProperty] private IComparisonStrategy? _selectedStrategy;
    [ObservableProperty] private string _sourceName = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _threshold = DefaultThreshold;

    // Для режима компаний: выбранный ключ стратегии («company_exact_name» и т.п.).
    [ObservableProperty] private string _selectedCompanyStrategyKey = DuplicateFinderService.CompanyFuzzyNameKey;

    // Справочник стратегий поиска дубликатов организаций (для ComboBox в XAML).
    public IReadOnlyList<DuplicateFinderService.CompanyStrategyOption> CompanyStrategies
        => DuplicateFinderService.CompanyStrategies;

    public async Task InitializeAsync()
    {
        if (IsCompanyMode)
        {
            await SearchCompanyAsync();
            return;
        }

        try
        {
            var result = await _duplicateFinderService.FindAsync(
                _sourcePersonId, SelectedStrategy?.Name, Threshold);
            if (result is null)
            {
                Notification.Show("Ошибка", $"Личность Id={_sourcePersonId} не найдена", NotificationType.Error);
                return;
            }
            SourceName = result.Source.FullName ?? $"Аноним #{result.Source.Id}";
            foreach (var c in result.Candidates) Candidates.Add(c);
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsCompanyMode)
        {
            await SearchCompanyAsync();
            return;
        }

        if (SelectedStrategy is null) return;
        IsBusy = true;
        try
        {
            var result = await _duplicateFinderService.FindAsync(
                _sourcePersonId, SelectedStrategy.Name, Threshold);
            Candidates.Clear();
            if (result is not null)
            {
                SourceName = result.Source.FullName ?? $"Аноним #{result.Source.Id}";
                foreach (var c in result.Candidates) Candidates.Add(c);
            }
        }
        catch (Exception ex) { Notification.Show("Ошибка поиска", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    // Поиск дубликатов организации через DuplicateFinderService.
    private async Task SearchCompanyAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _duplicateFinderService.FindCompanyAsync(
                _sourceCompanyId, SelectedCompanyStrategyKey, Threshold);
            CompanyCandidates.Clear();
            if (result is null)
            {
                Notification.Show("Ошибка", $"Организация Id={_sourceCompanyId} не найдена", NotificationType.Error);
                return;
            }

            SourceName = result.Source.Name;
            foreach (var c in result.Candidates) CompanyCandidates.Add(c);
        }
        catch (Exception ex) { Notification.Show("Ошибка поиска", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }
}
