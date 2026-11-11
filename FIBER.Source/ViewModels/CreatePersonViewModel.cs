using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Services;
using FIBER.Source.Services.MediaManagement;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class CreatePersonViewModel : ObservableValidator
{
    private readonly IPersonService _personService;
    private readonly ICountryService _countryService;
    private readonly ICompanyService _companyService;
    private readonly IEventService _eventService;
    private readonly IMediaService _mediaService;
    private readonly IMediaManagementService _mediaManagement;
    private readonly INotificationService _notificationService;

    public CreatePersonViewModel(
        IPersonService personService,
        ICountryService countryService,
        ICompanyService companyService,
        IEventService eventService,
        IMediaService mediaService,
        IMediaManagementService mediaManagement,
        INotificationService notificationService)
    {
        _personService = personService;
        _countryService = countryService;
        _companyService = companyService;
        _eventService = eventService;
        _mediaService = mediaService;
        _mediaManagement = mediaManagement;
        _notificationService = notificationService;
    }

    public ObservableCollection<Country> Countries { get; } = new();
    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();
    public ObservableCollection<PersonRelationshipEditVM> Relationships { get; } = new();
    public ObservableCollection<CompanyPersonEditVM> CompanyPeople { get; } = new();
    public ObservableCollection<EventParticipantEditVM> EventParticipations { get; } = new();
    public ObservableCollection<PersonSummary> AvailablePersons { get; } = new();
    public ObservableCollection<CompanySummary> AvailableCompanies { get; } = new();
    public ObservableCollection<EventSummary> AvailableEvents { get; } = new();

    /// <summary>Id только что созданной личности (для привязки существующих медиа).</summary>
    public int? CreatedOwnerId { get; private set; }

    [ObservableProperty] private int? _selectedRelatedPersonId;
    [ObservableProperty] private int? _selectedTargetCompanyId;
    [ObservableProperty] private int? _selectedTargetEventId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdentifiedMode))]
    private bool _isAnonymousMode;

    public bool IsIdentifiedMode => !IsAnonymousMode;

    /// <summary>Варианты выбора пола.</summary>
    public IReadOnlyList<GenderOption> GenderChoices { get; } = GenderOptions.All;

    [ObservableProperty] private Gender _selectedGender = Gender.Unknown;

    [ObservableProperty] private string _job = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string? _countryCode;
    [ObservableProperty] private string _car = string.Empty;
    [ObservableProperty] private string _mail = string.Empty;
    [ObservableProperty] private string _socialMedia = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _fullName = string.Empty;

    [ObservableProperty] private DateTime? _birthDate;
    [ObservableProperty] private string _passport = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _primaryAlias = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isBusy;

    public async Task InitializeAsync()
    {
        var countries = await _countryService.GetActiveAsync();
        Countries.Clear();
        foreach (var c in countries) Countries.Add(c);

        // Справочник персон для выбора цели связи.
        AvailablePersons.Clear();
        foreach (var p in await _personService.SearchAsync(string.Empty))
            AvailablePersons.Add(p);

        // Справочник организаций для связи «персона ↔ организация».
        AvailableCompanies.Clear();
        foreach (var c in await _companyService.ListAsync(null))
            AvailableCompanies.Add(c);

        // Справочник событий для связи «персона ↔ событие».
        AvailableEvents.Clear();
        foreach (var e in await _eventService.ListAsync(null))
            AvailableEvents.Add(e);
    }

    public async Task HandleFilesDropAsync(string[] files)
    {
        await _mediaManagement.HandleFilesDropAsync(TempFiles, files);
    }

    [RelayCommand]
    private void RemoveTempFile(MediaItemViewModel item)
    {
        if (item == null) return;
        TempFiles.Remove(item);
        _mediaService.DeleteTempFiles(new[] { item.ToDto() });
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        CreatedOwnerId = null;
        IsBusy = true;
        try
        {
            int personId;

            if (IsAnonymousMode)
            {
                if (string.IsNullOrWhiteSpace(PrimaryAlias))
                {
                    _notificationService.Show("Ошибка", "Укажите хотя бы один псевдоним", NotificationType.Warning);
                    return;
                }

                var input = new AnonymousPersonInput(
                    PrimaryAlias: PrimaryAlias.Trim(),
                    Notes: Notes.Trim(),
                    Gender: SelectedGender,
                    Job: Job.Trim(),
                    Phone: Phone.Trim(),
                    City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(),
                    Mail: Mail.Trim(),
                    SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(),
                    Aliases: null,
                    Relationships: BuildRelationshipsInput(),
                    CompanyPeople: BuildCompanyPeopleInput(),
                    EventParticipations: BuildEventParticipationsInput());

                var person = await _personService.CreateAnonymousAsync(input);
                personId = person.Id;
                _notificationService.Show("Успех",
                    $"Создана неидентифицированная личность\nId = {person.Id}\nПсевдоним = {PrimaryAlias}",
                    NotificationType.Success);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    _notificationService.Show("Ошибка", "Для идентифицированной личности ФИО обязательно", NotificationType.Warning);
                    return;
                }

                PersonService.SplitFullName(FullName.Trim(),
                    out string lastName, out string firstName, out string? secondName);

                var input = new PersonInput(
                    LastName: lastName,
                    FirstName: firstName,
                    SecondName: secondName,
                    Gender: SelectedGender,
                    BirthDate: BirthDate,
                    Passport: Passport.Trim(),
                    Job: Job.Trim(),
                    Phone: Phone.Trim(),
                    City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(),
                    Mail: Mail.Trim(),
                    SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(),
                    Notes: Notes.Trim(),
                    Status: IdentityStatus.Identified,
                    Aliases: null,
                    Relationships: BuildRelationshipsInput(),
                    CompanyPeople: BuildCompanyPeopleInput(),
                    EventParticipations: BuildEventParticipationsInput());

                var person = await _personService.CreateIdentifiedAsync(input);
                personId = person.Id;
                _notificationService.Show("Успех",
                    $"Создана идентифицированная личность\nId = {person.Id}\nФИО = {FullName}",
                    NotificationType.Success);
            }

            if (TempFiles.Count > 0)
            {
                // Существующие медиа (уже в БД) — привязать к владельцу
                var existingIds = TempFiles
                    .Where(m => m.MediaId.HasValue)
                    .Select(m => m.MediaId!.Value)
                    .ToList();
                if (existingIds.Count > 0)
                {
                    await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Person, personId);
                }

                // Новые медиа — закоммитить в БД
                var newMedia = TempFiles.Where(m => !m.MediaId.HasValue).ToList();
                if (newMedia.Count > 0)
                {
                    await _mediaService.CommitTempFilesAsync(
                        newMedia.Select(m => m.ToDto()).ToList(), MediaOwnerType.Person, personId);
                }

                TempFiles.Clear();
            }

            CreatedOwnerId = personId;
            ClearForm();
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка создания: {ex.Message}", NotificationType.Error);
        }
        finally { IsBusy = false; }
    }

    private bool CanCreate() => !IsBusy;

    [RelayCommand]
    private async Task AddExistingMedia()
    {
        // Передаём уже добавленные MediaId, чтобы они отмечались галочкой и не дублировались
        var alreadyAdded = TempFiles.Where(m => m.MediaId.HasValue).Select(m => m.MediaId!.Value).ToList();
        var selected = await _mediaManagement.SelectExistingMediaAsync(alreadyAdded);
        if (selected.Count == 0) return;

        // Добавление во временные файлы с защитой от дублей
        await _mediaManagement.AddToTempFilesAsync(TempFiles, selected);

        _notificationService.Show("Успех", $"Добавлено медиафайлов: {selected.Count}", NotificationType.Success);
    }

    // ----- Связи «персона ↔ персона» при создании -----

    [RelayCommand]
    private void AddRelationship()
    {
        if (SelectedRelatedPersonId == null) return;

        var target = AvailablePersons.FirstOrDefault(p => p.Id == SelectedRelatedPersonId.Value);
        if (target == null) return;

        // Защита от дубля: такая цель уже есть в списке связей.
        if (Relationships.Any(r => r.RelatedPersonId == target.Id))
        {
            _notificationService.Show("Внимание", "Связь с этой персоной уже добавлена", NotificationType.Warning);
            SelectedRelatedPersonId = null;
            return;
        }

        Relationships.Add(new PersonRelationshipEditVM
        {
            RelatedPersonId = target.Id,
            DisplayName = target.DisplayName,
            Kind = PersonRelationKind.Friend
        });
        SelectedRelatedPersonId = null;
    }

    [RelayCommand]
    private void RemoveRelationship(PersonRelationshipEditVM relationship)
    {
        if (relationship == null) return;
        Relationships.Remove(relationship);
    }

    private List<PersonRelationshipEditInput> BuildRelationshipsInput() =>
        Relationships
            .Where(r => r.RelatedPersonId.HasValue)
            .Select(r => r.ToInput())
            .ToList();

    // ----- Связи «персона ↔ организация» при создании -----

    [RelayCommand]
    private void AddCompanyPerson()
    {
        if (SelectedTargetCompanyId == null) return;

        var target = AvailableCompanies.FirstOrDefault(c => c.Id == SelectedTargetCompanyId.Value);
        if (target == null) return;

        // Защита от дубля: такая организация уже есть в списке связей.
        if (CompanyPeople.Any(cp => cp.CompanyId == target.Id))
        {
            _notificationService.Show("Внимание", "Связь с этой организацией уже добавлена", NotificationType.Warning);
            SelectedTargetCompanyId = null;
            return;
        }

        CompanyPeople.Add(new CompanyPersonEditVM
        {
            CompanyId = target.Id,
            CompanyName = target.Name,
            Role = "Сотрудник"
        });
        SelectedTargetCompanyId = null;
    }

    [RelayCommand]
    private void RemoveCompanyPerson(CompanyPersonEditVM companyPerson)
    {
        if (companyPerson == null) return;
        CompanyPeople.Remove(companyPerson);
    }

    private List<CompanyPersonEditInput> BuildCompanyPeopleInput() =>
        CompanyPeople
            .Where(cp => cp.CompanyId.HasValue)
            .Select(cp => cp.ToInput())
            .ToList();

    // ----- Связи «персона ↔ событие» при создании -----

    [RelayCommand]
    private void AddEventParticipation()
    {
        if (SelectedTargetEventId == null) return;

        var target = AvailableEvents.FirstOrDefault(e => e.Id == SelectedTargetEventId.Value);
        if (target == null) return;

        // Защита от дубля: такое событие уже есть в списке.
        if (EventParticipations.Any(ep => ep.EventId == target.Id))
        {
            _notificationService.Show("Внимание", "Связь с этим событием уже добавлена", NotificationType.Warning);
            SelectedTargetEventId = null;
            return;
        }

        EventParticipations.Add(new EventParticipantEditVM
        {
            PersonId = null, // Будет установлено при создании персоны в сервисе
            EventId = target.Id,
            DisplayName = target.Title,
            Role = EventParticipantRole.Participant,
            Notes = string.Empty
        });
        SelectedTargetEventId = null;
    }

    [RelayCommand]
    private void RemoveEventParticipation(EventParticipantEditVM eventParticipation)
    {
        if (eventParticipation == null) return;
        EventParticipations.Remove(eventParticipation);
    }

    private List<EventParticipantEditInput> BuildEventParticipationsInput() =>
        EventParticipations
            .Where(ep => ep.EventId.HasValue)
            .Select(ep => ep.ToEditInput())
            .ToList();

    public void ClearForm()
    {
        IsAnonymousMode = false;
        FullName = string.Empty;
        BirthDate = null;
        Passport = string.Empty;
        PrimaryAlias = string.Empty;
        SelectedGender = Gender.Unknown;
        Job = string.Empty;
        Phone = string.Empty;
        City = string.Empty;
        CountryCode = null;
        Car = string.Empty;
        Mail = string.Empty;
        SocialMedia = string.Empty;
        Address = string.Empty;
        Notes = string.Empty;

        _mediaService.DeleteTempFiles(TempFiles.Select(m => m.ToDto()));
        TempFiles.Clear();
        Relationships.Clear();
        CompanyPeople.Clear();
        EventParticipations.Clear();
        AvailablePersons.Clear();
        AvailableCompanies.Clear();
        AvailableEvents.Clear();
        SelectedRelatedPersonId = null;
        SelectedTargetCompanyId = null;
        SelectedTargetEventId = null;
    }
}