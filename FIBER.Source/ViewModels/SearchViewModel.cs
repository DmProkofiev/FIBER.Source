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
    private readonly IRelationService _relationService;
    private readonly ISearchStrategyFactory _strategyFactory;

    public event Action<int>? UpdatePersonRequested;
    public event Action<int>? UpdateCompanyRequested;
    public event Action<int>? UpdateEventRequested;

    // Переход по связи на целевой объект (из списка связей в карточке записи).
    public event Action<int>? NavigateToPersonRequested;
    public event Action<int>? NavigateToCompanyRequested;
    public event Action<int>? NavigateToEventRequested;

    public SearchViewModel(
        IPersonService personService,
        ICompanyService companyService,
        IEventService eventService,
        ICountryService countryService,
        IMediaService mediaService,
        IRelationService relationService,
        ISearchStrategyFactory strategyFactory)
    {
        _personService = personService;
        _companyService = companyService;
        _eventService = eventService;
        _countryService = countryService;
        _mediaService = mediaService;
        _relationService = relationService;
        _strategyFactory = strategyFactory;

        _selectedStatusFilter = StatusFilters.First();
    }

    public ObservableCollection<Country> Countries { get; } = new();
    public ObservableCollection<PersonSummary> PersonResults { get; } = new();
    public ObservableCollection<MediaItemViewModel> PersonImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> PersonVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> PersonAudio { get; } = new();
    public ObservableCollection<RelationItemViewModel> PersonRelations { get; } = new();

    // Индексы вкладок TabControl в SearchView.xaml.
    private const int PersonTabIndex = 0;
    private const int CompanyTabIndex = 1;
    private const int EventTabIndex = 2;

    /// <summary>Маршрут переходов по связям: объекты (тип + id), с которых ушли.</summary>
    private readonly Stack<NavigationHistoryEntry> _navigationRoute = new();

    /// <summary>Количество шагов в маршруте — управляет видимостью кнопки «Назад».</summary>
    public int NavigationHistoryCount => _navigationRoute.Count;

    /// <summary>Активная вкладка экрана поиска (лица / организации / события).</summary>
    [ObservableProperty] private int _selectedTabIndex;

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
    public string SelectedPersonDisplayName =>
        SelectedPerson == null
            ? string.Empty
            : SelectedPerson.FullName ?? $"Аноним #{SelectedPerson.Id}";

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
    public ObservableCollection<RelationItemViewModel> CompanyRelations { get; } = new();

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
    public ObservableCollection<RelationItemViewModel> EventRelations { get; } = new();

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
        await EnsureCountriesAsync();

        if (SearchStrategies.Count == 0)
        {
            // Фиксированный порядок стратегий в списке (словарь фабрики не гарантирует порядок).
            var orderedKeys = new[] { "fullname", "fullname_birthdate", "id", "passport", "phone", "alias" };
            var all = _strategyFactory.All.ToList();
            foreach (var key in orderedKeys)
                foreach (var s in all.Where(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)))
                    SearchStrategies.Add(s);
            foreach (var s in all.Where(x => !orderedKeys.Contains(x.Key, StringComparer.OrdinalIgnoreCase)))
                SearchStrategies.Add(s);
            SelectedStrategy = SearchStrategies.FirstOrDefault(s => s.Key == "fullname")
                               ?? SearchStrategies.FirstOrDefault();
        }

        await LoadPersonsAsync();
        await LoadCompaniesAsync();
        await LoadEventsAsync();
    }

    [RelayCommand] private async Task SearchPersonsAsync() => await LoadPersonsAsync();

    // Справочник стран нужен карточке личности (название страны по коду).
    private async Task EnsureCountriesAsync()
    {
        if (Countries.Count > 0) return;
        var countries = await _countryService.GetActiveAsync();
        foreach (var c in countries) Countries.Add(c);
    }

    private async Task LoadPersonsAsync()
    {
        ResetNavigationRoute();
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
        ResetNavigationRoute();
        await LoadPersonAsync(summary.Id);
    }

    private async Task LoadPersonAsync(int personId)
    {
        try
        {
            SelectedPerson = await _personService.GetByIdAsync(personId);
            if (SelectedPerson == null)
            {
                ClearPersonSelection();
                Notification.Show("Запись не найдена",
                    $"Личность Id={personId} не найдена или была удалена",
                    NotificationType.Warning);
                return;
            }

            await LoadMediaAsync(MediaOwnerType.Person, SelectedPerson.Id, PersonImages, PersonVideos, PersonAudio);
            await LoadPersonRelationsAsync(SelectedPerson);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearPersonSelection() { DisposeMedia(PersonImages, PersonVideos, PersonAudio); PersonRelations.Clear(); SelectedPerson = null; }

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
            await _personService.DeleteAsync(id); // удаление медиа-линков внутри сервиса (см. ТЗ §5.3)
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
        ResetNavigationRoute();
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
        ResetNavigationRoute();
        await LoadCompanyAsync(summary.Id);
    }

    private async Task LoadCompanyAsync(int companyId)
    {
        try
        {
            SelectedCompany = await _companyService.GetByIdAsync(companyId);
            if (SelectedCompany == null)
            {
                ClearCompanySelection();
                Notification.Show("Запись не найдена",
                    $"Организация Id={companyId} не найдена или была удалена",
                    NotificationType.Warning);
                return;
            }

            await LoadMediaAsync(MediaOwnerType.Company, SelectedCompany.Id, CompanyImages, CompanyVideos, CompanyAudio);
            await LoadCompanyRelationsAsync(SelectedCompany);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearCompanySelection() { DisposeMedia(CompanyImages, CompanyVideos, CompanyAudio); CompanyRelations.Clear(); SelectedCompany = null; }

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
        ResetNavigationRoute();
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
        ResetNavigationRoute();
        await LoadEventAsync(summary.Id);
    }

    private async Task LoadEventAsync(int eventId)
    {
        try
        {
            SelectedEvent = await _eventService.GetByIdAsync(eventId);
            if (SelectedEvent == null)
            {
                ClearEventSelection();
                Notification.Show("Запись не найдена",
                    $"Событие Id={eventId} не найдено или было удалено",
                    NotificationType.Warning);
                return;
            }

            await LoadMediaAsync(MediaOwnerType.Event, SelectedEvent.Id, EventImages, EventVideos, EventAudio);
            await LoadEventRelationsAsync(SelectedEvent);
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearEventSelection() { DisposeMedia(EventImages, EventVideos, EventAudio); EventRelations.Clear(); SelectedEvent = null; }

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

    // ===== Навигация по связям =====

    /// <summary>Показывает карточку личности в режиме просмотра (переход по связи).</summary>
    public async Task ShowPersonAsync(int personId)
    {
        SelectedTabIndex = PersonTabIndex;
        await EnsureCountriesAsync();
        await LoadPersonAsync(personId);
    }

    /// <summary>Показывает карточку организации в режиме просмотра (переход по связи).</summary>
    public async Task ShowCompanyAsync(int companyId)
    {
        SelectedTabIndex = CompanyTabIndex;
        await LoadCompanyAsync(companyId);
    }

    /// <summary>Показывает карточку события в режиме просмотра (переход по связи).</summary>
    public async Task ShowEventAsync(int eventId)
    {
        SelectedTabIndex = EventTabIndex;
        await LoadEventAsync(eventId);
    }

    /// <summary>
    /// Переход к объекту-дубликату: текущий объект сохраняется в маршруте,
    /// после чего открывается целевой — кнопка «Назад» вернёт к исходной карточке.
    /// </summary>
    public void NavigateToDuplicate(RelationTargetType targetType, int targetId)
    {
        PushCurrentToRoute();
        NavigateToTarget(targetType, targetId);
    }

    /// <summary>Возвращает на предыдущий объект маршрута переходов по связям.</summary>
    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private async Task NavigateBackAsync()
    {
        if (_navigationRoute.Count == 0) return;

        var entry = _navigationRoute.Pop();
        UpdateNavigationState();

        await ShowEntryAsync(entry);
    }

    private bool CanNavigateBack() => _navigationRoute.Count > 0;

    // Переход по связи: текущий объект сохраняется в маршрут, затем открывается целевой.
    [RelayCommand] private void NavigateRelation(RelationItemViewModel? item)
    {
        if (item == null || !item.TargetId.HasValue || !item.TargetType.HasValue) return;

        PushCurrentToRoute();
        NavigateToTarget(item.TargetType.Value, item.TargetId.Value);
    }

    private async Task ShowEntryAsync(NavigationHistoryEntry entry)
    {
        switch (entry.TargetType)
        {
            case RelationTargetType.Person:
                await ShowPersonAsync(entry.TargetId);
                break;
            case RelationTargetType.Company:
                await ShowCompanyAsync(entry.TargetId);
                break;
            case RelationTargetType.Event:
                await ShowEventAsync(entry.TargetId);
                break;
        }
    }

    // Сохраняет в маршрут объект, с которого выполняется переход (если он выбран).
    private void PushCurrentToRoute()
    {
        var current = CurrentEntry();
        if (current is null) return;

        _navigationRoute.Push(current);
        UpdateNavigationState();
    }

    private NavigationHistoryEntry? CurrentEntry()
    {
        if (SelectedPerson != null) return new(RelationTargetType.Person, SelectedPerson.Id);
        if (SelectedCompany != null) return new(RelationTargetType.Company, SelectedCompany.Id);
        if (SelectedEvent != null) return new(RelationTargetType.Event, SelectedEvent.Id);
        return null;
    }

    private void NavigateToTarget(RelationTargetType targetType, int targetId)
    {
        switch (targetType)
        {
            case RelationTargetType.Person:
                NavigateToPersonRequested?.Invoke(targetId);
                break;
            case RelationTargetType.Company:
                NavigateToCompanyRequested?.Invoke(targetId);
                break;
            case RelationTargetType.Event:
                NavigateToEventRequested?.Invoke(targetId);
                break;
        }
    }

    // Сбрасывает маршрут при новом поиске или выборе записи из списка.
    private void ResetNavigationRoute()
    {
        if (_navigationRoute.Count == 0) return;
        _navigationRoute.Clear();
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        OnPropertyChanged(nameof(NavigationHistoryCount));
        NavigateBackCommand.NotifyCanExecuteChanged();
    }

    // ===== Связи: только отображение в SearchView (CRUD — в режиме редактирования) =====

    private static string PersonDisplayName(Person? p) =>
        p?.FullName ?? p?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value ?? (p != null ? $"Аноним #{p.Id}" : "—");

    // Дополнительные сведения о связи: только заполненные части, через разделитель.
    private static string RelationDetails(params string?[] parts) =>
        string.Join(" • ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string NotesDetails(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? string.Empty : $"Заметки: {notes.Trim()}";

    private async Task LoadPersonRelationsAsync(Person person)
    {
        PersonRelations.Clear();

        foreach (var r in await _relationService.ListRelationshipsAsync(person.Id))
        {
            var related = r.RelatedPerson;
            if (related is null) continue;
            PersonRelations.Add(new RelationItemViewModel(
                r.KindDisplay,
                PersonDisplayName(related),
                RelationTargetType.Person,
                related.Id,
                NotesDetails(r.Notes)));
        }

        foreach (var ep in await _relationService.ListEventParticipantsAsync(person.Id))
        {
            var evt = ep.Event;
            if (evt is null) continue;
            PersonRelations.Add(new RelationItemViewModel(
                "Участие в событии",
                evt.Title,
                RelationTargetType.Event,
                evt.Id,
                RelationDetails($"Роль: {ep.RoleDisplay}", NotesDetails(ep.Notes))));
        }

    }

    private async Task LoadCompanyRelationsAsync(Company company)
    {
        CompanyRelations.Clear();

        // Взаимодействия «компания ↔ компания»: прямые и обратные (из навигационных свойств GetByIdAsync).
        foreach (var ci in company.OutgoingInteractions)
        {
            var target = ci.TargetCompany;
            if (target is null) continue;
            CompanyRelations.Add(new RelationItemViewModel(
                string.IsNullOrWhiteSpace(ci.InteractionType) ? "Взаимодействие" : ci.InteractionType,
                target.Name,
                RelationTargetType.Company,
                target.Id,
                NotesDetails(ci.Notes)));
        }

        foreach (var ci in company.IncomingInteractions)
        {
            var source = ci.SourceCompany;
            if (source is null) continue;
            CompanyRelations.Add(new RelationItemViewModel(
                string.IsNullOrWhiteSpace(ci.InteractionType) ? "Взаимодействие (обратная)" : ci.InteractionType + " (обратная)",
                source.Name,
                RelationTargetType.Company,
                source.Id,
                NotesDetails(ci.Notes)));
        }

        foreach (var ep in await _relationService.ListParticipantsByCompanyAsync(company.Id))
        {
            var evt = ep.Event;
            if (evt is null) continue;
            CompanyRelations.Add(new RelationItemViewModel($"Событие: {ep.RoleDisplay}",
                evt.Title,
                RelationTargetType.Event,
                evt.Id,
                NotesDetails(ep.Notes)));
        }

    }

    private async Task LoadEventRelationsAsync(Event evt)
    {
        EventRelations.Clear();

        if (evt.OrganizerPerson != null)
            EventRelations.Add(new RelationItemViewModel("Организатор", PersonDisplayName(evt.OrganizerPerson),
                RelationTargetType.Person, evt.OrganizerPerson.Id));
        if (evt.OrganizerCompany != null)
            EventRelations.Add(new RelationItemViewModel("Организатор", evt.OrganizerCompany.Name,
                RelationTargetType.Company, evt.OrganizerCompany.Id));

        foreach (var ep in await _relationService.ListParticipantsByEventAsync(evt.Id))
        {
            if (ep.Person != null)
            {
                EventRelations.Add(new RelationItemViewModel($"Участник: {ep.RoleDisplay}", PersonDisplayName(ep.Person),
                    RelationTargetType.Person, ep.Person.Id, NotesDetails(ep.Notes)));
            }
            else if (ep.Company != null)
            {
                EventRelations.Add(new RelationItemViewModel($"Участник: {ep.RoleDisplay}", ep.Company.Name,
                    RelationTargetType.Company, ep.Company.Id, NotesDetails(ep.Notes)));
            }
        }

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

/// <summary>Шаг маршрута переходов по связям: тип объекта и его идентификатор.</summary>
public sealed record NavigationHistoryEntry(RelationTargetType TargetType, int TargetId);