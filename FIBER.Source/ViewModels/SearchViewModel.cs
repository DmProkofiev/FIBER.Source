using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using System;
using System.Collections.Generic;
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
    private readonly IDuplicateFinderService _duplicateFinderService;
    private readonly IExportService _exportService;
    private readonly INotificationService _notificationService;

    public event Action<int>? UpdatePersonRequested;
    public event Action<int>? UpdateCompanyRequested;
    public event Action<int>? UpdateEventRequested;

    // Переход по связи на целевой объект (из списка связей в карточке записи).
    public event Action<int>? NavigateToPersonRequested;
    public event Action<int>? NavigateToCompanyRequested;
    public event Action<int>? NavigateToEventRequested;

    // Открыть окно дубликатов для персоны
    public event Action<int>? DuplicatesRequested;
    // Экспорт объекта
    public event Action<MediaOwnerType, int, string>? ExportRequested;
    // Зум изображения
    public event Action<MediaItemViewModel>? ImageZoomRequested;
    // Зум видео
    public event Action<MediaItemViewModel>? VideoZoomRequested;

    public SearchViewModel(
        IPersonService personService,
        ICompanyService companyService,
        IEventService eventService,
        ICountryService countryService,
        IMediaService mediaService,
        IRelationService relationService,
        ISearchStrategyFactory strategyFactory,
        IDuplicateFinderService duplicateFinderService,
        IExportService exportService,
        INotificationService notificationService)
    {
        _personService = personService;
        _companyService = companyService;
        _eventService = eventService;
        _countryService = countryService;
        _mediaService = mediaService;
        _relationService = relationService;
        _strategyFactory = strategyFactory;
        _duplicateFinderService = duplicateFinderService;
        _exportService = exportService;
        _notificationService = notificationService;

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

    // ===== Унифицированные свойства для EntityDetailsPanel (устранение дублирования SearchView) =====

    /// <summary>Отображаемое имя выбранной сущности (для любой вкладки).</summary>
    [ObservableProperty] private string _selectedEntityDisplayName = string.Empty;

    /// <summary>Поля для отображения в унифицированной панели деталей.</summary>
    public ObservableCollection<FieldViewModel> SelectedEntityFields { get; } = new();

    /// <summary>Связи выбранной сущности (общие для любой вкладки).</summary>
    public ObservableCollection<RelationItemViewModel> SelectedEntityRelations { get; } = new();

    /// <summary>Признак того, что выбрана какая-то запись (любой вкладки).</summary>
    public bool HasEntitySelection => SelectedPerson != null || SelectedCompany != null || SelectedEvent != null;

    /// <summary>Текущий тип выбранной сущности.</summary>
    public EntityType SelectedEntityType { get; private set; } = EntityType.None;

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
            _notificationService.Show("Ошибка", $"Ошибка поиска: {ex.Message}", NotificationType.Error);
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
                _notificationService.Show("Запись не найдена",
                    $"Личность Id={personId} не найдена или была удалена",
                    NotificationType.Warning);
                return;
            }

            await LoadMediaAsync(MediaOwnerType.Person, SelectedPerson.Id, PersonImages, PersonVideos, PersonAudio);
            await LoadPersonRelationsAsync(SelectedPerson);
            BuildEntityFieldsForPerson(SelectedPerson);
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearPersonSelection() => ClearSelection(() =>
    {
        DisposeMedia(PersonImages, PersonVideos, PersonAudio);
        PersonRelations.Clear();
        SelectedPerson = null;
    });

    [RelayCommand]
    private void UpdatePerson() { if (SelectedPerson != null) UpdatePersonRequested?.Invoke(SelectedPerson.Id); }

    [RelayCommand]
    private async Task DeleteAliasAsync(PersonAlias? alias)
    {
        if (alias == null || SelectedPerson == null) return;
        if (!_notificationService.Confirm("Подтверждение", $"Удалить псевдоним «{alias.Value}»?")) return;

        try
        {
            var personId = SelectedPerson.Id;
            await _personService.DeleteAliasAsync(personId, alias.Id);
            SelectedPerson = await _personService.GetByIdAsync(personId);
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task DeletePersonAsync()
    {
        if (SelectedPerson == null) return;
        if (!_notificationService.Confirm("Подтверждение", $"Удалить запись?\n\n{SelectedPersonDisplayName}")) return;

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
            _notificationService.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
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
            _notificationService.Show("Ошибка", $"Ошибка поиска: {ex.Message}", NotificationType.Error);
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
                _notificationService.Show("Запись не найдена",
                    $"Организация Id={companyId} не найдена или была удалена",
                    NotificationType.Warning);
                return;
            }

            await LoadMediaAsync(MediaOwnerType.Company, SelectedCompany.Id, CompanyImages, CompanyVideos, CompanyAudio);
            await LoadCompanyRelationsAsync(SelectedCompany);
            BuildEntityFieldsForCompany(SelectedCompany);
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearCompanySelection() => ClearSelection(() =>
    {
        DisposeMedia(CompanyImages, CompanyVideos, CompanyAudio);
        CompanyRelations.Clear();
        SelectedCompany = null;
    });

    [RelayCommand]
    private void UpdateCompany() { if (SelectedCompany != null) UpdateCompanyRequested?.Invoke(SelectedCompany.Id); }

    [RelayCommand]
    private async Task DeleteCompanyAsync()
    {
        if (SelectedCompany == null) return;
        if (!_notificationService.Confirm("Подтверждение", $"Удалить организацию?\n\n{SelectedCompanyDisplayName}")) return;

        IsCompanyBusy = true;
        try
        {
            await _companyService.DeleteAsync(SelectedCompany.Id);
            ClearCompanySelection();
            await LoadCompaniesAsync();
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
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
            _notificationService.Show("Ошибка", $"Ошибка поиска: {ex.Message}", NotificationType.Error);
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
                _notificationService.Show("Запись не найдена",
                    $"Событие Id={eventId} не найдено или было удалено",
                    NotificationType.Warning);
                return;
            }

            await LoadMediaAsync(MediaOwnerType.Event, SelectedEvent.Id, EventImages, EventVideos, EventAudio);
            await LoadEventRelationsAsync(SelectedEvent);
            BuildEntityFieldsForEvent(SelectedEvent);
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка загрузки: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void ClearEventSelection() => ClearSelection(() =>
    {
        DisposeMedia(EventImages, EventVideos, EventAudio);
        EventRelations.Clear();
        SelectedEvent = null;
    });

    [RelayCommand]
    private void UpdateEvent() { if (SelectedEvent != null) UpdateEventRequested?.Invoke(SelectedEvent.Id); }

    [RelayCommand]
    private async Task DeleteEventAsync()
    {
        if (SelectedEvent == null) return;
        if (!_notificationService.Confirm("Подтверждение", $"Удалить событие?\n\n{SelectedEventDisplayName}")) return;

        IsEventBusy = true;
        try
        {
            await _eventService.DeleteAsync(SelectedEvent.Id);
            ClearEventSelection();
            await LoadEventsAsync();
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
        finally { IsEventBusy = false; }
    }

    [RelayCommand]
    private async Task ShowDuplicatesAsync()
    {
        if (SelectedPerson == null) return;
        var result = await _duplicateFinderService.FindAsync(
            SelectedPerson.Id, null, DuplicateFinderViewModel.DefaultThreshold);
        if (result is null) return;

        // Событие для открытия окна дубликатов (обрабатывается во View или MainViewModel)
        DuplicatesRequested?.Invoke(SelectedPerson.Id);
    }

    [RelayCommand]
    private async Task ExportPersonAsync()
    {
        if (SelectedPerson == null) return;
        ExportRequested?.Invoke(MediaOwnerType.Person, SelectedPerson.Id,
            $"Личность: {SelectedPerson.FullName ?? $"Аноним #{SelectedPerson.Id}"}");
    }

    [RelayCommand]
    private async Task ExportCompanyAsync()
    {
        if (SelectedCompany == null) return;
        ExportRequested?.Invoke(MediaOwnerType.Company, SelectedCompany.Id,
            $"Организация: {SelectedCompany.Name}");
    }

    [RelayCommand]
    private async Task ExportEventAsync()
    {
        if (SelectedEvent == null) return;
        ExportRequested?.Invoke(MediaOwnerType.Event, SelectedEvent.Id,
            $"Событие: {SelectedEvent.Title}");
    }

    [RelayCommand]
    private void ZoomImage(MediaItemViewModel media)
    {
        if (media == null || media.ImageSource == null) return;
        ImageZoomRequested?.Invoke(media);
    }

    [RelayCommand]
    private void ZoomVideo(MediaItemViewModel media)
    {
        if (media == null || string.IsNullOrEmpty(media.FilePath)) return;
        VideoZoomRequested?.Invoke(media);
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

        // Исходящие связи (персона — инициатор).
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

        // Обратные связи (другая персона — инициатор, эта персона — цель).
        foreach (var r in await _relationService.ListReverseRelationshipsAsync(person.Id))
        {
            var source = r.Person;
            if (source is null) continue;
            PersonRelations.Add(new RelationItemViewModel(
                PersonRelationKinds.DisplayNameOfKind(r.Kind),
                PersonDisplayName(source),
                RelationTargetType.Person,
                source.Id,
                NotesDetails(r.Notes)));
        }

        // Связи персоны с организациями.
        foreach (var cp in await _relationService.ListPersonCompaniesAsync(person.Id))
        {
            var company = cp.Company;
            if (company is null) continue;
            PersonRelations.Add(new RelationItemViewModel(
                cp.RoleDisplay,
                company.Name,
                RelationTargetType.Company,
                company.Id,
                NotesDetails(cp.Notes)));
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

        // Связи организации с персонами.
        foreach (var cp in await _relationService.ListCompanyPeopleAsync(company.Id))
        {
            var person = cp.Person;
            if (person is null) continue;
            CompanyRelations.Add(new RelationItemViewModel(
                cp.RoleDisplay,
                PersonDisplayName(person),
                RelationTargetType.Person,
                person.Id,
                NotesDetails(cp.Notes)));
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

        // Связи события с другими событиями (исходящие).
        foreach (var ee in await _relationService.ListEventEventsAsync(evt.Id))
        {
            var target = ee.TargetEvent;
            if (target is null) continue;
            EventRelations.Add(new RelationItemViewModel(
                string.IsNullOrWhiteSpace(ee.RelationshipType) ? "Связано с" : ee.RelationshipType,
                target.Title,
                RelationTargetType.Event,
                target.Id,
                NotesDetails(ee.Notes)));
        }

        // Связи события с другими событиями (обратные).
        foreach (var ee in await _relationService.ListReverseEventEventsAsync(evt.Id))
        {
            var source = ee.SourceEvent;
            if (source is null) continue;
            EventRelations.Add(new RelationItemViewModel(
                string.IsNullOrWhiteSpace(ee.RelationshipType) ? "Связано с (обратная)" : ee.RelationshipType + " (обратная)",
                source.Title,
                RelationTargetType.Event,
                source.Id,
                NotesDetails(ee.Notes)));
        }

        foreach (var ep in await _relationService.ListParticipantsByEventAsync(evt.Id))
        {
            if (ep.Person != null)
            {
                EventRelations.Add(new RelationItemViewModel($"Роль: {ep.RoleDisplay}", PersonDisplayName(ep.Person),
                    RelationTargetType.Person, ep.Person.Id, NotesDetails(ep.Notes)));
            }
            else if (ep.Company != null)
            {
                EventRelations.Add(new RelationItemViewModel($"Роль: {ep.RoleDisplay}", ep.Company.Name,
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
            foreach (var dto in mediaList)
                dto.ToViewModel(_mediaService).Dispose();
            return;
        }

        foreach (var dto in mediaList)
        {
            var vm = dto.ToViewModel(_mediaService);
            if (dto.IsImage) images.Add(vm);
            else if (dto.IsVideo) videos.Add(vm);
            else if (dto.IsAudio) audio.Add(vm);

            // Декодируем изображение в фоне, не блокируя GUI.
            if (dto.IsImage)
                _ = vm.LoadImageAsync();
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

    /// <summary>Общая логика очистки выделения для любой вкладки.</summary>
    private void ClearSelection(Action clearSpecific)
    {
        clearSpecific();
        ClearEntityFields();
    }

    // ===== Унифицированные поля для EntityDetailsPanel =====

    private void ClearEntityFields()
    {
        SelectedEntityFields.Clear();
        SelectedEntityRelations.Clear();
        SelectedEntityDisplayName = string.Empty;
        SelectedEntityType = EntityType.None;
        OnPropertyChanged(nameof(HasEntitySelection));
    }

    private void BuildEntityFieldsForPerson(Person person)
    {
        BeginEntityFields(EntityType.Person, SelectedPersonDisplayName, PersonRelations);

        AddField("ID", person.Id.ToString());
        AddField("Фамилия", person.LastName);
        AddField("Имя", person.FirstName);
        AddField("Отчество", person.SecondName);
        AddField("Статус", person.Status == IdentityStatus.Identified ? "Идентифицирован" : "Не идентифицирован");
        AddField("Пол", person.GenderDisplay);
        AddField("Дата рождения", person.BirthDateDisplay);
        AddField("Паспорт", person.Passport);
        AddField("Профессия", person.Job);
        AddField("Телефон", person.Phone);
        AddField("Город", person.City);
        AddField("Страна", person.Country?.NameRu ?? "—");
        AddField("Транспорт", person.Car);
        AddField("Email", person.Mail);
        AddField("Соцсети", person.SocialMedia);
        AddField("Адрес", person.Address);
        AddField("Заметки", person.Notes);

        if (person.Aliases.Count > 0)
        {
            AddField("Псевдонимы", string.Join(", ", person.Aliases.Select(a => $"{(a.IsPrimary ? "*" : "")}{a.Value}")));
        }

        EndEntityFields();
    }

    private void BuildEntityFieldsForCompany(Company company)
    {
        BeginEntityFields(EntityType.Company, SelectedCompanyDisplayName, CompanyRelations);

        AddField("ID", company.Id.ToString());
        AddField("Название", company.Name);
        AddField("Рег. номер", company.RegistrationNumber);
        AddField("Адрес", company.Address);
        AddField("Телефон", company.Phone);
        AddField("Email", company.Mail);
        AddField("Сайт", company.Website);
        AddField("Заметки", company.Notes);

        EndEntityFields();
    }

    private void BuildEntityFieldsForEvent(Event evt)
    {
        BeginEntityFields(EntityType.Event, SelectedEventDisplayName, EventRelations);

        AddField("ID", evt.Id.ToString());
        AddField("Название", evt.Title);
        AddField("Дата", evt.DateDisplay);
        AddField("Организатор", evt.OrganizerDisplay);
        AddField("Описание", evt.Description);
        AddField("Заметки", evt.Notes);

        EndEntityFields();
    }

    private void BeginEntityFields(EntityType type, string displayName, IEnumerable<RelationItemViewModel> relations)
    {
        SelectedEntityType = type;
        SelectedEntityDisplayName = displayName;
        SelectedEntityFields.Clear();
        SelectedEntityRelations.Clear();
        foreach (var r in relations) SelectedEntityRelations.Add(r);
    }

    private void EndEntityFields()
    {
        OnPropertyChanged(nameof(HasEntitySelection));
    }

    private void AddField(string label, string? value)
    {
        SelectedEntityFields.Add(new FieldViewModel
        {
            Label = label,
            Value = value ?? "Сведения отсутствуют"
        });
    }
}

/// <summary>Шаг маршрута переходов по связям: тип объекта и его идентификатор.</summary>
public sealed record NavigationHistoryEntry(RelationTargetType TargetType, int TargetId);