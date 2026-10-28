using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public sealed class DuplicateCandidate
{
    public Person Person { get; init; } = null!;
    public double Score { get; init; }
    public string DisplayName => Person.FullName ?? $"Аноним #{Person.Id}";
    public string BirthDate => Person.BirthDate?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";
    public string StatusText => Person.Status.ToString();
    public string ScoreText => $"{Score * 100:0}%";
}

public partial class DuplicateFinderViewModel : ObservableObject
{
    private readonly IPersonService _personService;
    private readonly IComparisonStrategyFactory _strategyFactory;
    private readonly int _sourcePersonId;
    private Person? _sourcePerson;

    public DuplicateFinderViewModel(int personId, IPersonService personService, IComparisonStrategyFactory strategyFactory)
    {
        _sourcePersonId = personId;
        _personService = personService;
        _strategyFactory = strategyFactory;
        Strategies = new ObservableCollection<IComparisonStrategy>(_strategyFactory.All);
        _selectedStrategy = Strategies.FirstOrDefault();
    }

    public ObservableCollection<IComparisonStrategy> Strategies { get; }
    public ObservableCollection<DuplicateCandidate> Candidates { get; } = new();

    [ObservableProperty] private IComparisonStrategy? _selectedStrategy;
    [ObservableProperty] private string _sourcePersonName = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public async Task InitializeAsync()
    {
        try
        {
            _sourcePerson = await _personService.GetByIdAsync(_sourcePersonId);
            if (_sourcePerson is null)
            {
                Notification.Show("Ошибка", $"Личность Id={_sourcePersonId} не найдена", NotificationType.Error);
                return;
            }
            SourcePersonName = _sourcePerson.FullName ?? $"Аноним #{_sourcePerson.Id}";
            await SearchAsync();
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (_sourcePerson is null || SelectedStrategy is null) return;
        IsBusy = true;
        try
        {
            var all = await _personService.GetAllExceptAsync(_sourcePersonId);
            var results = new List<DuplicateCandidate>(all.Count);
            foreach (var candidate in all)
            {
                double score;
                try { score = SelectedStrategy.Compare(_sourcePerson, candidate); }
                catch { score = 0; }
                if (score > 0) results.Add(new DuplicateCandidate { Person = candidate, Score = score });
            }
            Candidates.Clear();
            foreach (var c in results.OrderByDescending(c => c.Score)) Candidates.Add(c);
        }
        catch (Exception ex) { Notification.Show("Ошибка поиска", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }
}