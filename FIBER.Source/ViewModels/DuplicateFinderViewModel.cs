using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;

namespace FIBER.Source.ViewModels;

public partial class DuplicateFinderViewModel : ObservableObject
{
    public const double DefaultThreshold = 0.6;

    private readonly IDuplicateFinderService _duplicateFinderService;
    private readonly int _sourcePersonId;

    public DuplicateFinderViewModel(int personId, IDuplicateFinderService duplicateFinderService)
    {
        _sourcePersonId = personId;
        _duplicateFinderService = duplicateFinderService;
        Strategies = new ObservableCollection<IComparisonStrategy>(duplicateFinderService.Strategies);
        _selectedStrategy = Strategies.FirstOrDefault();
        _threshold = DefaultThreshold;
    }

    public ObservableCollection<IComparisonStrategy> Strategies { get; }
    public ObservableCollection<DuplicateCandidate> Candidates { get; } = new();

    [ObservableProperty] private IComparisonStrategy? _selectedStrategy;
    [ObservableProperty] private string _sourcePersonName = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _threshold = DefaultThreshold;

    public async Task InitializeAsync()
    {
        try
        {
            var result = await _duplicateFinderService.FindAsync(
                _sourcePersonId, SelectedStrategy?.Name, Threshold);
            if (result is null)
            {
                Notification.Show("Ошибка", $"Личность Id={_sourcePersonId} не найдена", NotificationType.Error);
                return;
            }
            SourcePersonName = result.Source.FullName ?? $"Аноним #{result.Source.Id}";
            foreach (var c in result.Candidates) Candidates.Add(c);
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (SelectedStrategy is null) return;
        IsBusy = true;
        try
        {
            var result = await _duplicateFinderService.FindAsync(
                _sourcePersonId, SelectedStrategy.Name, Threshold);
            Candidates.Clear();
            if (result is not null)
            {
                SourcePersonName = result.Source.FullName ?? $"Аноним #{result.Source.Id}";
                foreach (var c in result.Candidates) Candidates.Add(c);
            }
        }
        catch (Exception ex) { Notification.Show("Ошибка поиска", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }
}
