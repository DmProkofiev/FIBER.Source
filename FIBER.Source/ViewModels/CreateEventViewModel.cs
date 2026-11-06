using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.MediaManagement;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class CreateEventViewModel : ObservableValidator
{
    private readonly IEventService _eventService;
    private readonly ICompanyService _companyService;
    private readonly IPersonService _personService;
    private readonly IMediaService _mediaService;
    private readonly IMediaManagementService _mediaManagement;

    public CreateEventViewModel(
        IEventService eventService,
        ICompanyService companyService,
        IPersonService personService,
        IMediaService mediaService,
        IMediaManagementService mediaManagement)
    {
        _eventService = eventService;
        _companyService = companyService;
        _personService = personService;
        _mediaService = mediaService;
        _mediaManagement = mediaManagement;
    }

    public ObservableCollection<CompanySummary> Companies { get; } = new();
    public ObservableCollection<PersonSummary> Persons { get; } = new();
    public ObservableCollection<EventParticipantEditVM> PendingParticipants { get; } = new();
    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();

    /// <summary>Id только что созданного события (для привязки существующих медиа).</summary>
    public int? CreatedOwnerId { get; private set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Название обязательно")]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _title = string.Empty;

    [ObservableProperty] private DateTime? _date = DateTime.Today;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty] private int? _organizerCompanyId;
    [ObservableProperty] private int? _organizerPersonId;

    [ObservableProperty] private int? _selectedPersonId;
    [ObservableProperty] private int? _selectedCompanyId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isBusy;

    public async Task InitializeAsync()
    {
        var companies = await _companyService.ListAsync(null);
        Companies.Clear();
        foreach (var c in companies) Companies.Add(c);

        var persons = await _personService.SearchAsync(string.Empty);
        Persons.Clear();
        foreach (var p in persons) Persons.Add(p);
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
        _mediaService.DeleteTempFiles(new[] { item });
    }

    [RelayCommand]
    private void AddPersonParticipant()
    {
        if (SelectedPersonId == null) return;
        var person = Persons.FirstOrDefault(p => p.Id == SelectedPersonId.Value);
        if (person == null) return;
        if (PendingParticipants.Any(p => p.PersonId == person.Id)) return;

        PendingParticipants.Add(EventParticipantEditVM.ForPerson(person.Id, person.DisplayName));
        SelectedPersonId = null;
    }

    [RelayCommand]
    private void AddCompanyParticipant()
    {
        if (SelectedCompanyId == null) return;
        var company = Companies.FirstOrDefault(c => c.Id == SelectedCompanyId.Value);
        if (company == null) return;
        if (PendingParticipants.Any(p => p.CompanyId == company.Id)) return;

        PendingParticipants.Add(EventParticipantEditVM.ForCompany(company.Id, company.Name));
        SelectedCompanyId = null;
    }

    [RelayCommand]
    private void RemoveParticipant(EventParticipantEditVM item)
    {
        if (item == null) return;
        PendingParticipants.Remove(item);
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        CreatedOwnerId = null;
        ValidateAllProperties();
        if (HasErrors) return;

        IsBusy = true;
        try
        {
            var input = new EventInput(
                Title.Trim(),
                Date,
                Description.Trim(),
                Notes.Trim(),
                OrganizerCompanyId,
                OrganizerPersonId,
                PendingParticipants.Select(p => p.ToInput()).ToList());

            var ev = await _eventService.CreateAsync(input);

            if (TempFiles.Count > 0)
            {
                // Существующие медиа (уже в БД) — привязать к владельцу
                var existingIds = TempFiles
                    .Where(m => m.MediaId.HasValue)
                    .Select(m => m.MediaId!.Value)
                    .ToList();
                if (existingIds.Count > 0)
                {
                    await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Event, ev.Id);
                }

                // Новые медиа — закоммитить в БД
                var newMedia = TempFiles.Where(m => !m.MediaId.HasValue).ToList();
                if (newMedia.Count > 0)
                {
                    await _mediaService.CommitTempFilesAsync(newMedia, MediaOwnerType.Event, ev.Id);
                }

                TempFiles.Clear();
            }

            CreatedOwnerId = ev.Id;
            Notification.Show("Успех",
                $"Создано событие\nId = {ev.Id}\nНазвание = {ev.Title}",
                NotificationType.Success);
            ClearForm();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка создания: {ex.Message}", NotificationType.Error);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddExistingMedia()
    {
        var selected = await _mediaManagement.SelectExistingMediaAsync();
        if (selected.Count == 0) return;

        foreach (var media in selected)
        {
            if (!TempFiles.Any(t => t.MediaId == media.MediaId))
                TempFiles.Add(media);
        }

        Notification.Show("Успех", $"Добавлено медиафайлов: {selected.Count}", NotificationType.Success);
    }

    private bool CanCreate() => !IsBusy;

    public void ClearForm()
    {
        Title = string.Empty;
        Date = DateTime.Today;
        Description = string.Empty;
        Notes = string.Empty;
        OrganizerCompanyId = null;
        OrganizerPersonId = null;
        PendingParticipants.Clear();
        SelectedPersonId = null;
        SelectedCompanyId = null;
        _mediaService.DeleteTempFiles(TempFiles);
        TempFiles.Clear();
    }
}