using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IPersonService _personService;
    private readonly ICompanyService _companyService;
    private readonly IEventService _eventService;
    private readonly ICountryService _countryService;
    private readonly IMediaService _mediaService;
    private readonly ISearchStrategyFactory _strategyFactory;

    public event Action<int>? UpdatePersonRequested;
    public event Action<int>? UpdateCompanyRequested;
    public event Action<int>? UpdateEventRequested;

    public SearchViewModel(
        IPersonService personService,
        ICompanyService companyService,
        IEventService eventService,
        ICountryService countryService,
        IMediaService mediaService,
        ISearchStrategyFactory strategyFactory)
    {
        _personService = personService;
        _companyService = companyService;
        _eventService = eventService;
        _countryService = countryService;
        _mediaService = mediaService;
        _strategyFactory = strategyFactory;

        _selectedStatusFilter = StatusFilters.First();
    }

    public ObservableCollection<Country> Countries { get; } = new();
    public ObservableCollection<PersonSummary> PersonResults { get; } = new();
    public ObservableCollection<MediaItemViewModel> PersonImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> PersonVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> PersonAudio { get; } = new();

    public ObservableCollection<IdentityFilter> StatusFilters { get; } = new()
    {
        new(null, "Все статусы"),
        new(false, "Не идентифицирован"),
        new(true, "Идентифицирован")
    };

    public ObservableCollection<ISearchStrategy> SearchStrategies { get; } = new();

    [ObservableProperty] private IdentityFilter? _selectedStatusFilter;
    [ObservableProperty] private ISearchStrategy? _selectedStrategy;
    [ObservableProperty] private string _personQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPersonSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedPersonDisplayName))]
    [NotifyPropertyChangedFor(nameof(SelectedPersonCountryName))]
    private Person? _selectedPerson;

    [ObservableProperty] private bool _isPersonBusy;

    public bool HasPersonSelection => SelectedPerson != null;
    public string SelectedPersonDisplayName => SelectedPerson?.FullName ?? $"Аноним #{SelectedPerson?.Id}";

    public string SelectedPersonCountryName
    {
        get
        {
            if (SelectedPerson?.CountryCode == null) return "Сведения отсутствуют";
            var country = Countries.FirstOrDefault(c => c.Code == SelectedPerson.CountryCode);
            return country?.NameRu ?? SelectedPerson.CountryCode;
        }
    }

    public ObservableCollection<CompanySummary> CompanyResults { get; } = new();
    public ObservableCollection<MediaItemViewModel> CompanyImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> CompanyVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> CompanyAudio { get; } = new();

    [ObservableProperty] private string _companyQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompanySelection))]
    [NotifyPropertyChangedFor(nameof(SelectedCompanyDisplayName))]
    private Company? _selectedCompany;

    [ObservableProperty] private bool _isCompanyBusy;

    public bool HasCompanySelection => SelectedCompany != null;
    public string SelectedCompanyDisplayName => SelectedCompany?.Name ?? string.Empty;

    public ObservableCollection<EventSummary> EventResults { get; } = new();
    public ObservableCollection<MediaItemViewModel> EventImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> EventVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> EventAudio { get; } = new();

    [ObservableProperty] private string _eventQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEventSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedEventDisplayName))]
    private Event? _selectedEvent;

    [ObservableProperty] private bool _isEventBusy;

    public bool HasEventSelection => SelectedEvent != null;
    public string SelectedEventDisplayName => SelectedEvent?.Title ?? string.Empty;

    public async Task InitializeAsync()
    {
        if (Countries.Count == 0)
        {
            var countries = await _countryService.GetActiveAsync();
            foreach (var c in countries) Countries.Add(c);
        }

        if (SearchStrategies.Count == 0)
        {
            foreach (var s in _strategyFactory.All) SearchStrategies.Add(s);
            SelectedStrategy = SearchStrategies.FirstOrDefault(s => s.Key == "fullname")
                               ?? SearchStrategies.FirstOrDefault();
        }

        await LoadPersonsAsync();
        await LoadCompaniesAsync();
        await LoadEventsAsync();
    }

    [RelayCommand] private async Task SearchPersonsAsync() => await LoadPersonsAsync();

    private async Task LoadPersonsAsync()
    {
        IsPersonBusy = true;
        try
        {
            DisposeMedia(PersonImages, PersonVideos, PersonAudio);
            var results = await _personService.SearchAsync(
                PersonQuery, SelectedStrategy?.Key, SelectedStatusFilter?.IsIdentified);
            PersonResults.Clear();
            foreach (var r in results) PersonResults.Add(r);
            SelectedPerson = null;
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка поиска: {ex.Message}", NotificationType.Error);
        }
        finally { IsPersonBusy = false; }
    }

    [RelayCommand]
    private async Task SelectPersonAsync(PersonSummary summary)
    {
        if (summary == null) return;
        try
        {
            SelectedPerson = await _personService.GetByIdAsync(summary.Id);
            if (SelectedPerson != null)
                await LoadMediaAsync(MediaOwnerType.Person, SelectedPerson.Id, PersonImages, PersonVideos, PersonAudio);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearPersonSelection() { DisposeMedia(PersonImages, PersonVideos, PersonAudio); SelectedPerson = null; }

    [RelayCommand]
    private void UpdatePerson() { if (SelectedPerson != null) UpdatePersonRequested?.Invoke(SelectedPerson.Id); }

    [RelayCommand]
    private async Task DeleteAliasAsync(PersonAlias? alias)
    {
        if (alias == null || SelectedPerson == null) return;
        if (!Notification.Confirm("Подтверждение", $"Удалить псевдоним «{alias.Value}»?")) return;

        try
        {
            var personId = SelectedPerson.Id;
            await _personService.DeleteAliasAsync(personId, alias.Id);
            SelectedPerson = await _personService.GetByIdAsync(personId);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task DeletePersonAsync()
    {
        if (SelectedPerson == null) return;
        if (!Notification.Confirm("Подтверждение", $"Удалить запись?\n\n{SelectedPersonDisplayName}")) return;

        IsPersonBusy = true;
        try
        {
            var id = SelectedPerson.Id;
            await _personService.DeleteAsync(id);
            await _mediaService.RemoveAllLinksForOwnerAsync(MediaOwnerType.Person, id);
            ClearPersonSelection();
            await LoadPersonsAsync();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
        finally { IsPersonBusy = false; }
    }

    [RelayCommand] private async Task SearchCompaniesAsync() => await LoadCompaniesAsync();

    private async Task LoadCompaniesAsync()
    {
        IsCompanyBusy = true;
        try
        {
            DisposeMedia(CompanyImages, CompanyVideos, CompanyAudio);
            var results = await _companyService.ListAsync(CompanyQuery);
            CompanyResults.Clear();
            foreach (var r in results) CompanyResults.Add(r);
            SelectedCompany = null;
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка поиска: {ex.Message}", NotificationType.Error);
        }
        finally { IsCompanyBusy = false; }
    }

    [RelayCommand]
    private async Task SelectCompanyAsync(CompanySummary summary)
    {
        if (summary == null) return;
        try
        {
            SelectedCompany = await _companyService.GetByIdAsync(summary.Id);
            if (SelectedCompany != null)
                await LoadMediaAsync(MediaOwnerType.Company, SelectedCompany.Id, CompanyImages, CompanyVideos, CompanyAudio);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearCompanySelection() { DisposeMedia(CompanyImages, CompanyVideos, CompanyAudio); SelectedCompany = null; }

    [RelayCommand]
    private void UpdateCompany() { if (SelectedCompany != null) UpdateCompanyRequested?.Invoke(SelectedCompany.Id); }

    [RelayCommand]
    private async Task DeleteCompanyAsync()
    {
        if (SelectedCompany == null) return;
        if (!Notification.Confirm("Подтверждение", $"Удалить организацию?\n\n{SelectedCompanyDisplayName}")) return;

        IsCompanyBusy = true;
        try
        {
            await _companyService.DeleteAsync(SelectedCompany.Id);
            ClearCompanySelection();
            await LoadCompaniesAsync();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
        finally { IsCompanyBusy = false; }
    }

    [RelayCommand] private async Task SearchEventsAsync() => await LoadEventsAsync();

    private async Task LoadEventsAsync()
    {
        IsEventBusy = true;
        try
        {
            DisposeMedia(EventImages, EventVideos, EventAudio);
            var results = await _eventService.ListAsync(EventQuery);
            EventResults.Clear();
            foreach (var r in results) EventResults.Add(r);
            SelectedEvent = null;
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка поиска: {ex.Message}", NotificationType.Error);
        }
        finally { IsEventBusy = false; }
    }

    [RelayCommand]
    private async Task SelectEventAsync(EventSummary summary)
    {
        if (summary == null) return;
        try
        {
            SelectedEvent = await _eventService.GetByIdAsync(summary.Id);
            if (SelectedEvent != null)
                await LoadMediaAsync(MediaOwnerType.Event, SelectedEvent.Id, EventImages, EventVideos, EventAudio);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearEventSelection() { DisposeMedia(EventImages, EventVideos, EventAudio); SelectedEvent = null; }

    [RelayCommand]
    private void UpdateEvent() { if (SelectedEvent != null) UpdateEventRequested?.Invoke(SelectedEvent.Id); }

    [RelayCommand]
    private async Task DeleteEventAsync()
    {
        if (SelectedEvent == null) return;
        if (!Notification.Confirm("Подтверждение", $"Удалить событие?\n\n{SelectedEventDisplayName}")) return;

        IsEventBusy = true;
        try
        {
            await _eventService.DeleteAsync(SelectedEvent.Id);
            ClearEventSelection();
            await LoadEventsAsync();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
        finally { IsEventBusy = false; }
    }

    private async Task LoadMediaAsync(
        MediaOwnerType ownerType, int ownerId,
        ObservableCollection<MediaItemViewModel> images,
        ObservableCollection<MediaItemViewModel> videos,
        ObservableCollection<MediaItemViewModel> audio)
    {
        DisposeMedia(images, videos, audio);
        var mediaList = await _mediaService.GetMediaForOwnerAsync(ownerType, ownerId);

        bool stillRelevant = ownerType switch
        {
            MediaOwnerType.Person => SelectedPerson?.Id == ownerId,
            MediaOwnerType.Company => SelectedCompany?.Id == ownerId,
            MediaOwnerType.Event => SelectedEvent?.Id == ownerId,
            _ => false
        };

        if (!stillRelevant)
        {
            foreach (var vm in mediaList) vm.Dispose();
            return;
        }

        foreach (var vm in mediaList)
        {
            if (vm.IsImage) images.Add(vm);
            else if (vm.IsVideo) videos.Add(vm);
            else if (vm.IsAudio) audio.Add(vm);
        }
    }

    private static void DisposeMedia(
        ObservableCollection<MediaItemViewModel> images,
        ObservableCollection<MediaItemViewModel> videos,
        ObservableCollection<MediaItemViewModel> audio)
    {
        foreach (var i in images) i.Dispose();
        foreach (var v in videos) v.Dispose();
        foreach (var a in audio) a.Dispose();
        images.Clear(); videos.Clear(); audio.Clear();
    }
}