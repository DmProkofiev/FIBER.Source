using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Services.MediaManagement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using FIBER.Source.Services;

namespace FIBER.Source.ViewModels;

public partial class UpdatePersonViewModel : ObservableValidator
{
    private readonly IPersonService _personService;
    private readonly ICountryService _countryService;
    private readonly ICompanyService _companyService;
    private readonly IEventService _eventService;
    private readonly IMediaService _mediaService;
    private readonly IMediaManagementService _mediaManagement;
    private readonly INotificationService _notificationService;

    public event Action? Saved;
    public event Action? Cancelled;

    public UpdatePersonViewModel(
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
    public ObservableCollection<MediaItemViewModel> ExistingImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingAudio { get; } = new();
    public ObservableCollection<PersonAliasEditVM> Aliases { get; } = new();
    // Связи «персона ↔ персона»: исходящие редактируются, обратные показаны для информации.
    public ObservableCollection<PersonRelationshipEditVM> Relationships { get; } = new();
    // Справочник персон для ComboBox выбора цели новой связи.
    public ObservableCollection<Contracts.Summaries.PersonSummary> AvailablePersons { get; } = new();
    // Связи «персона ↔ организация».
    public ObservableCollection<CompanyPersonEditVM> CompanyPeople { get; } = new();
    // Справочник организаций для связи с персоной.
    public ObservableCollection<CompanySummary> AvailableCompanies { get; } = new();
    // Связи «персона ↔ событие».
    public ObservableCollection<EventParticipantEditVM> EventParticipations { get; } = new();
    // Справочник событий для связи с персоной.
    public ObservableCollection<EventSummary> AvailableEvents { get; } = new();

    [ObservableProperty] private int? _selectedRelatedPersonId;
    [ObservableProperty] private int? _selectedTargetCompanyId;
    [ObservableProperty] private int? _selectedTargetEventId;

    [ObservableProperty] private int _personId;
    [ObservableProperty] private IdentityStatus _currentStatus;
    [ObservableProperty] private bool _isInPromoteMode;
    [ObservableProperty] private bool _isBusy;

    public bool IsIdentified => CurrentStatus >= IdentityStatus.Identified;
    public bool ShowIdentifiedFields => IsIdentified || IsInPromoteMode;
    public bool ShowAnonymousFields => !IsIdentified && !IsInPromoteMode;
    public bool ShowPromoteButton => !IsIdentified && !IsInPromoteMode;

    public string StatusDisplay => IsIdentified ? "Идентифицирована" : "Не идентифицирована";

    /// <summary>Варианты выбора пола.</summary>
    public IReadOnlyList<GenderOption> GenderChoices { get; } = GenderOptions.All;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Фамилия обязательна")]
    private string _lastName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Имя обязательно")]
    private string _firstName = string.Empty;

    [ObservableProperty] private string _secondName = string.Empty;

    [ObservableProperty] private Gender _selectedGender = Gender.Unknown;

    [ObservableProperty] private DateTime? _birthDate;
    [ObservableProperty] private string _passport = string.Empty;

    [ObservableProperty] private string _primaryAlias = string.Empty;

    [ObservableProperty] private string _job = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string? _countryCode;
    [ObservableProperty] private string _car = string.Empty;
    [ObservableProperty] private string _mail = string.Empty;
    [ObservableProperty] private string _socialMedia = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    partial void OnCurrentStatusChanged(IdentityStatus value)
    {
        OnPropertyChanged(nameof(IsIdentified));
        OnPropertyChanged(nameof(ShowIdentifiedFields));
        OnPropertyChanged(nameof(ShowAnonymousFields));
        OnPropertyChanged(nameof(ShowPromoteButton));
        OnPropertyChanged(nameof(StatusDisplay));
    }

    partial void OnIsInPromoteModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowIdentifiedFields));
        OnPropertyChanged(nameof(ShowAnonymousFields));
        OnPropertyChanged(nameof(ShowPromoteButton));
    }

    public async Task LoadAsync(int personId)
    {
        _mediaService.DeleteTempFiles(TempFiles.Select(m => m.ToDto()));
        TempFiles.Clear();
        ClearExistingMedia();
        Aliases.Clear();
        Relationships.Clear();
        SelectedRelatedPersonId = null;
        IsInPromoteMode = false;

        // Справочник персон для выбора цели связи (исключаем саму редактируемую персону).
        AvailablePersons.Clear();
        foreach (var p in await _personService.SearchAsync(string.Empty))
            if (p.Id != personId) AvailablePersons.Add(p);

        if (Countries.Count == 0)
        {
            var countries = await _countryService.GetActiveAsync();
            foreach (var c in countries) Countries.Add(c);
        }

        var person = await _personService.GetByIdAsync(personId);
        if (person == null)
        {
            _notificationService.Show("Ошибка", $"Личность Id = {personId} не найдена", NotificationType.Error);
            Cancelled?.Invoke();
            return;
        }

        PersonId = person.Id;
        CurrentStatus = person.Status;
        SelectedGender = person.Gender;

        // Опциональные поля в БД могут быть NULL — в TextBox показываем пусто.
        Job = person.Job ?? string.Empty;
        Phone = person.Phone ?? string.Empty;
        City = person.City ?? string.Empty;
        CountryCode = person.CountryCode;
        Car = person.Car ?? string.Empty;
        Mail = person.Mail ?? string.Empty;
        SocialMedia = person.SocialMedia ?? string.Empty;
        Address = person.Address ?? string.Empty;
        Notes = person.Notes ?? string.Empty;

        var primary = person.Aliases.FirstOrDefault(a => a.IsPrimary);

        if (IsIdentified)
        {
            LastName = person.LastName ?? string.Empty;
            FirstName = person.FirstName ?? string.Empty;
            SecondName = person.SecondName ?? string.Empty;
            BirthDate = person.BirthDate;
            Passport = person.Passport ?? string.Empty;
        }
        else
        {
            PrimaryAlias = primary?.Value ?? string.Empty;
        }

        foreach (var a in person.Aliases.Where(a => !a.IsPrimary))
            Aliases.Add(PersonAliasEditVM.FromModel(a));

        // Связи: сначала исходящие (редактируемые), затем обратные (только просмотр).
        foreach (var r in person.Relationships)
            Relationships.Add(PersonRelationshipEditVM.FromModel(r));
        foreach (var r in person.ReverseRelationships)
            Relationships.Add(PersonRelationshipEditVM.FromReverse(r));

        // Справочник организаций для связи «персона ↔ организация».
        AvailableCompanies.Clear();
        foreach (var c in await _companyService.ListAsync(null))
            AvailableCompanies.Add(c);

        // Связи персоны с организациями.
        CompanyPeople.Clear();
        foreach (var cp in person.CompanyPeople)
            CompanyPeople.Add(CompanyPersonEditVM.FromModel(cp));

        // Справочник событий для связи «персона ↔ событие».
        AvailableEvents.Clear();
        foreach (var e in await _eventService.ListAsync(null))
            AvailableEvents.Add(e);

        // Связи персоны с событиями.
        EventParticipations.Clear();
        foreach (var ep in person.EventParticipations)
            EventParticipations.Add(EventParticipantEditVM.FromModel(ep));

        await LoadExistingMediaAsync(personId);
    }

    private OwnerMedia? _ownerMedia;

    // Загрузка медиа владельца в коллекции UI (изображения лениво подгружаются в память).
    private async Task LoadExistingMediaAsync(int ownerId)
    {
        _mediaManagement.ClearMedia(_ownerMedia ?? new OwnerMedia());
        _ownerMedia = await _mediaManagement.LoadOwnerMediaAsync(MediaOwnerType.Person, ownerId);

        ExistingImages.Clear();
        ExistingVideos.Clear();
        ExistingAudio.Clear();
        foreach (var m in _ownerMedia.Images) ExistingImages.Add(m);
        foreach (var m in _ownerMedia.Videos) ExistingVideos.Add(m);
        foreach (var m in _ownerMedia.Audios) ExistingAudio.Add(m);
    }

    private void ClearExistingMedia()
    {
        _mediaManagement.ClearMedia(_ownerMedia ?? new OwnerMedia());
        _ownerMedia = null;
        ExistingImages.Clear(); ExistingVideos.Clear(); ExistingAudio.Clear();
        CompanyPeople.Clear();
        AvailableCompanies.Clear();
        SelectedTargetCompanyId = null;
        EventParticipations.Clear();
        AvailableEvents.Clear();
        SelectedTargetEventId = null;
    }

    [RelayCommand]
    private void AddAlias() => Aliases.Add(new PersonAliasEditVM { Type = AliasType.Alias });

    [RelayCommand]
    private void RemoveAlias(PersonAliasEditVM alias)
    {
        if (alias != null) Aliases.Remove(alias);
    }

    // ----- Связи «персона ↔ персона»: CRUD внутри редактора -----

    [RelayCommand]
    private void AddRelationship()
    {
        if (SelectedRelatedPersonId == null) return;

        var target = AvailablePersons.FirstOrDefault(p => p.Id == SelectedRelatedPersonId.Value);
        if (target == null) return;

        // Защита от дубля на уровне UI: такая цель уже есть в списке связей.
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
            .Where(r => !r.IsReverseOnly && r.RelatedPersonId.HasValue)
            .Select(r => r.ToInput())
            .ToList();

    // ----- Связи «персона ↔ организация» -----

    [RelayCommand]
    private void AddCompanyPerson()
    {
        if (SelectedTargetCompanyId == null) return;

        var target = AvailableCompanies.FirstOrDefault(c => c.Id == SelectedTargetCompanyId.Value);
        if (target == null) return;

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
            PersonId = PersonId,  // Текущая персона
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

    // ----- Связи «персона ↔ событие» -----

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
            PersonId = PersonId,  // Текущая персона
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

    [RelayCommand]
    private void EnterPromoteMode()
    {
        if (IsIdentified) return;
        IsInPromoteMode = true;

        // Псевдоним анонима подставляем в поля ФИО для удобства редактирования.
        PersonService.SplitFullName(PrimaryAlias.Trim(),
            out string promLastName, out string promFirstName, out string? promSecondName);
        LastName = promLastName;
        FirstName = promFirstName;
        SecondName = promSecondName ?? string.Empty;
    }

    public async Task HandleFilesDropAsync(string[] files)
    {
        await _mediaManagement.HandleFilesDropAsync(TempFiles, files);
    }

    [RelayCommand]
    private void RemoveTempFile(MediaItemViewModel item)
    {
        _mediaManagement.RemoveTempFile(TempFiles, item);
    }

    [RelayCommand]
    private async Task DeleteExistingMediaAsync(MediaItemViewModel media)
    {
        if (media == null) return;
        if (!_notificationService.Confirm("Подтверждение", $"Удалить файл {media.FileName}?")) return;

        try
        {
            bool deleted = await _mediaManagement.DeleteExistingMediaAsync(MediaOwnerType.Person, PersonId, media.ToDto());
            if (deleted)
            {
                ExistingImages.Remove(media);
                ExistingVideos.Remove(media);
                ExistingAudio.Remove(media);
            }
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task AddExistingMedia()
    {
        // Передаём уже добавленные MediaId из TempFiles, чтобы они отмечались галочкой и не дублировались
        var alreadyAdded = TempFiles.Where(m => m.MediaId.HasValue).Select(m => m.MediaId!.Value).ToList();
        var selected = await _mediaManagement.SelectExistingMediaAsync(MediaOwnerType.Person, PersonId, alreadyAdded);
        if (selected.Count == 0) return;

        // Все элементы из SelectExistingMediaAsync имеют MediaId (они загружены из БД),
        // поэтому newMedia всегда пуст — упрощаем логику.
        var existingIds = selected
            .Where(m => m.MediaId.HasValue)
            .Select(m => m.MediaId!.Value)
            .ToList();

        // Уже привязанные к этой личности файлы пропускаются — о защите сообщаем пользователю.
        int skipped = 0;
        if (existingIds.Count > 0)
        {
            skipped = await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Person, PersonId);
            // Перезагружаем существующие медиа, чтобы отобразить их в UI
            await LoadExistingMediaAsync(PersonId);
        }

        var added = existingIds.Count - skipped;
        if (skipped > 0)
        {
            _notificationService.Show("Внимание",
                $"{skipped} файл(ов) уже привязаны к этой личности и не добавлены повторно.",
                NotificationType.Warning);
        }
        if (added > 0)
        {
            _notificationService.Show("Успех", $"Привязано существующих медиа: {added}", NotificationType.Success);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var aliasesInput = Aliases
                .Where(a => !string.IsNullOrWhiteSpace(a.Value))
                .Select(a => a.ToInput())
                .ToList();

            if (IsInPromoteMode)
            {
                if (string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(FirstName))
                {
                    _notificationService.Show("Ошибка", "Укажите фамилию и имя для идентификации", NotificationType.Warning);
                    return;
                }

                // Прежний основной псевдоним анонима сохраняем как обычный псевдоним
                // (новый основной автоматически не создаётся — ФИО хранится в полях персоны).
                if (!string.IsNullOrWhiteSpace(PrimaryAlias) &&
                    !aliasesInput.Any(a => string.Equals(a.Value, PrimaryAlias.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    aliasesInput.Add(new PersonAliasInput(null, PrimaryAlias.Trim(), AliasType.Alias, false));
                }

                await _personService.PromoteAsync(PersonId, new PromotePersonInput(
                    LastName: LastName.Trim(),
                    FirstName: FirstName.Trim(),
                    SecondName: string.IsNullOrWhiteSpace(SecondName) ? null : SecondName.Trim(),
                    Gender: SelectedGender,
                    BirthDate: BirthDate,
                    Passport: Passport.Trim()));

                var personInput = new PersonInput(
                    LastName: LastName.Trim(), FirstName: FirstName.Trim(), SecondName: string.IsNullOrWhiteSpace(SecondName) ? null : SecondName.Trim(),
                    Gender: SelectedGender, BirthDate: BirthDate, Passport: Passport.Trim(),
                    Job: Job.Trim(), Phone: Phone.Trim(), City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(), Mail: Mail.Trim(), SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(), Notes: Notes.Trim(),
                    Status: IdentityStatus.Identified,
                    Aliases: aliasesInput, Relationships: BuildRelationshipsInput(),
                    CompanyPeople: BuildCompanyPeopleInput(),
                    EventParticipations: BuildEventParticipationsInput());

                await _personService.UpdateAsync(PersonId, personInput);
            }
            else if (IsIdentified)
            {
                if (string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(FirstName))
                {
                    _notificationService.Show("Ошибка", "Фамилия и имя обязательны", NotificationType.Warning);
                    return;
                }

                var input = new PersonInput(
                    LastName: LastName.Trim(), FirstName: FirstName.Trim(), SecondName: string.IsNullOrWhiteSpace(SecondName) ? null : SecondName.Trim(),
                    Gender: SelectedGender, BirthDate: BirthDate, Passport: Passport.Trim(),
                    Job: Job.Trim(), Phone: Phone.Trim(), City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(), Mail: Mail.Trim(), SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(), Notes: Notes.Trim(),
                    Status: IdentityStatus.Identified,
                    Aliases: aliasesInput, Relationships: BuildRelationshipsInput(),
                    CompanyPeople: BuildCompanyPeopleInput(),
                    EventParticipations: BuildEventParticipationsInput());

                await _personService.UpdateAsync(PersonId, input);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(PrimaryAlias))
                {
                    _notificationService.Show("Ошибка", "Псевдоним обязателен", NotificationType.Warning);
                    return;
                }

                var input = new AnonymousPersonInput(
                    PrimaryAlias: PrimaryAlias.Trim(),
                    Notes: Notes.Trim(),
                    Gender: SelectedGender,
                    Job: Job.Trim(), Phone: Phone.Trim(), City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(), Mail: Mail.Trim(), SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(),
                    Aliases: aliasesInput, Relationships: BuildRelationshipsInput(),
                    CompanyPeople: BuildCompanyPeopleInput(),
                    EventParticipations: BuildEventParticipationsInput());

                await _personService.UpdateAnonymousAsync(PersonId, input);
            }

            if (TempFiles.Count > 0)
            {
                // Существующие медиа — привязать к владельцу
                var existingIds = TempFiles
                    .Where(m => m.MediaId.HasValue)
                    .Select(m => m.MediaId!.Value)
                    .ToList();
                if (existingIds.Count > 0)
                {
                    await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Person, PersonId);
                }

                // Новые медиа — закоммитить в БД
                var newMedia = TempFiles.Where(m => !m.MediaId.HasValue).ToList();
                if (newMedia.Count > 0)
                {
                    await _mediaService.CommitTempFilesAsync(
                        newMedia.Select(m => m.ToDto()).ToList(), MediaOwnerType.Person, PersonId);
                }

                TempFiles.Clear();
            }

            _notificationService.Show("Успех", "Изменения сохранены", NotificationType.Success);
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            _notificationService.Show("Ошибка", $"Ошибка сохранения: {ex.Message}", NotificationType.Error);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel()
    {
        _mediaService.DeleteTempFiles(TempFiles.Select(m => m.ToDto()));
        TempFiles.Clear();
        ClearExistingMedia();
        Cancelled?.Invoke();
    }
}