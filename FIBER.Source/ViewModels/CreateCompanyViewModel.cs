using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.MediaManagement;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

// VM создания организации с медиа
public partial class CreateCompanyViewModel : ObservableValidator
{
    private readonly ICompanyService _companyService;
    private readonly IPersonService _personService;
    private readonly IEventService _eventService;
    private readonly IMediaService _mediaService;
    private readonly IMediaManagementService _mediaManagement;
    private readonly INotificationService _notificationService;

    // Внедряет сервисы
    public CreateCompanyViewModel(ICompanyService companyService, IPersonService personService, IEventService eventService, IMediaService mediaService, IMediaManagementService mediaManagement, INotificationService notificationService)
    {
        _companyService = companyService;
        _personService = personService;
        _eventService = eventService;
        _mediaService = mediaService;
        _mediaManagement = mediaManagement;
        _notificationService = notificationService;
    }

    public async Task InitializeAsync()
    {
        // Справочник организаций для связи «организация ↔ персона».
        AvailablePersons.Clear();
        foreach (var p in await _personService.SearchAsync(string.Empty))
            AvailablePersons.Add(p);

        // Справочник организаций для взаимодействий «организация → организация».
        AvailableCompanies.Clear();
        foreach (var c in await _companyService.GetAllExceptAsync(0))
            AvailableCompanies.Add(c);

        // Справочник событий для связи «организация ↔ событие».
        AvailableEvents.Clear();
        foreach (var e in await _eventService.ListAsync(null))
            AvailableEvents.Add(e);
    }

    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();
    public ObservableCollection<CompanyInteractionEditVM> Interactions { get; } = new();
    public ObservableCollection<CompanyPersonEditVM> CompanyPeople { get; } = new();
    public ObservableCollection<EventParticipantEditVM> EventParticipations { get; } = new();
    public ObservableCollection<Company> AvailableCompanies { get; } = new();
    public ObservableCollection<PersonSummary> AvailablePersons { get; } = new();
    public ObservableCollection<EventSummary> AvailableEvents { get; } = new();

    /// <summary>Id только что созданной организации (для привязки существующих медиа).</summary>
    public int? CreatedOwnerId { get; private set; }

    [ObservableProperty] private int? _selectedTargetCompanyId;
    [ObservableProperty] private int? _selectedTargetPersonId;
    [ObservableProperty] private int? _selectedTargetEventId;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Название обязательно")]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _name = string.Empty;

    [ObservableProperty] private string _registrationNumber = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _mail = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isBusy;

    // Принимает drag-and-drop файлов
    public async Task HandleFilesDropAsync(string[] files)
    {
        await _mediaManagement.HandleFilesDropAsync(TempFiles, files);
    }

    // Удаляет временный файл
    [RelayCommand]
    private void RemoveTempFile(MediaItemViewModel item)
    {
        if (item == null) return;
        TempFiles.Remove(item);
        _mediaService.DeleteTempFiles(new[] { item.ToDto() });
    }

    // Создаёт организацию и коммитит медиа
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        CreatedOwnerId = null;
        ValidateAllProperties();
        if (HasErrors) return;

        IsBusy = true;
        try
        {
            var input = new CompanyInput(
                Name.Trim(), RegistrationNumber.Trim(), Address.Trim(),
                Phone.Trim(), Mail.Trim(), Website.Trim(), Notes.Trim(),
                Interactions: BuildInteractionsInput(),
                CompanyPeople: BuildPersonCompaniesInput(),
                EventParticipations: BuildEventParticipationsInput());

            var company = await _companyService.CreateAsync(input);

            if (TempFiles.Count > 0)
            {
                // Существующие медиа (уже в БД) — привязать к владельцу
                var existingIds = TempFiles
                    .Where(m => m.MediaId.HasValue)
                    .Select(m => m.MediaId!.Value)
                    .ToList();
                if (existingIds.Count > 0)
                {
                    await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Company, company.Id);
                }

                // Новые медиа — закоммитить в БД
                var newMedia = TempFiles.Where(m => !m.MediaId.HasValue).ToList();
                if (newMedia.Count > 0)
                {
                    await _mediaService.CommitTempFilesAsync(
                        newMedia.Select(m => m.ToDto()).ToList(), MediaOwnerType.Company, company.Id);
                }

                TempFiles.Clear();
            }

            CreatedOwnerId = company.Id;
            _notificationService.Show("Успех",
                $"Создана организация\nId = {company.Id}\nНазвание = {company.Name}",
                NotificationType.Success);
            ClearForm();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка",
                $"Ошибка создания: {ex.Message}",
                NotificationType.Error);
        }
        finally { IsBusy = false; }
    }

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

    // ----- Взаимодействия «компания → компания» при создании -----

    [RelayCommand]
    private void AddInteraction()
    {
        if (SelectedTargetCompanyId == null) return;

        var target = AvailableCompanies.FirstOrDefault(c => c.Id == SelectedTargetCompanyId.Value);
        if (target == null) return;

        // Защита от дубля: такое взаимодействие уже есть в списке.
        if (Interactions.Any(i => i.TargetCompanyId == target.Id))
        {
            Notification.Show("Внимание", "Взаимодействие с этой организацией уже добавлено", NotificationType.Warning);
            SelectedTargetCompanyId = null;
            return;
        }

        Interactions.Add(new CompanyInteractionEditVM
        {
            TargetCompanyId = target.Id,
            DisplayName = target.Name,
            InteractionType = "Партнёр"
        });
        SelectedTargetCompanyId = null;
    }

    [RelayCommand]
    private void RemoveInteraction(CompanyInteractionEditVM interaction)
    {
        if (interaction == null) return;
        Interactions.Remove(interaction);
    }

    private List<CompanyInteractionEditInput> BuildInteractionsInput() =>
        Interactions
            .Where(i => i.TargetCompanyId.HasValue)
            .Select(i => i.ToInput())
            .ToList();

    // ----- Связи «организация ↔ персона» при создании -----

    [RelayCommand]
    private void AddPersonCompany()
    {
        if (SelectedTargetPersonId == null) return;

        var target = AvailablePersons.FirstOrDefault(p => p.Id == SelectedTargetPersonId.Value);
        if (target == null) return;

        // Защита от дубля: такая персона уже есть в списке связей.
        if (CompanyPeople.Any(cp => cp.PersonId == target.Id))
        {
            Notification.Show("Внимание", "Связь с этой персоной уже добавлена", NotificationType.Warning);
            SelectedTargetPersonId = null;
            return;
        }

        CompanyPeople.Add(new CompanyPersonEditVM
        {
            PersonId = target.Id,
            PersonName = target.DisplayName,
            Role = string.Empty
        });
        SelectedTargetPersonId = null;
    }

    [RelayCommand]
    private void RemovePersonCompany(CompanyPersonEditVM companyPerson)
    {
        if (companyPerson == null) return;
        CompanyPeople.Remove(companyPerson);
    }

    private List<CompanyPersonEditInput> BuildPersonCompaniesInput() =>
        CompanyPeople
            .Where(cp => cp.PersonId.HasValue)
            .Select(cp => cp.ToInput())
            .ToList();

    // ----- Связи «организация ↔ событие» при создании -----

    [RelayCommand]
    private void AddEventParticipation()
    {
        if (SelectedTargetEventId == null) return;

        var target = AvailableEvents.FirstOrDefault(e => e.Id == SelectedTargetEventId.Value);
        if (target == null) return;

        // Защита от дубля: такое событие уже есть в списке.
        if (EventParticipations.Any(ep => ep.EventId == target.Id))
        {
            Notification.Show("Внимание", "Связь с этим событием уже добавлена", NotificationType.Warning);
            SelectedTargetEventId = null;
            return;
        }

        EventParticipations.Add(new EventParticipantEditVM
        {
            CompanyId = null, // Будет установлено при создании организации в сервисе
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

    // Проверяет, можно ли запускать создание
    private bool CanCreate() => !IsBusy;

    // Очищает форму и освобождает временные файлы
    public void ClearForm()
    {
        Name = string.Empty;
        RegistrationNumber = string.Empty;
        Address = string.Empty;
        Phone = string.Empty;
        Mail = string.Empty;
        Website = string.Empty;
        Notes = string.Empty;
        _mediaService.DeleteTempFiles(TempFiles.Select(m => m.ToDto()));
        TempFiles.Clear();
        Interactions.Clear();
        CompanyPeople.Clear();
        AvailableCompanies.Clear();
        AvailablePersons.Clear();
        SelectedTargetCompanyId = null;
        SelectedTargetPersonId = null;
        SelectedTargetEventId = null;
        EventParticipations.Clear();
        AvailableEvents.Clear();
    }
}