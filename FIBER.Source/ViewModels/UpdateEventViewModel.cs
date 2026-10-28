using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class UpdateEventViewModel : ObservableValidator
{
    // ----- Зависимости -----

    private readonly IEventService _eventService;
    private readonly ICompanyService _companyService;
    private readonly IPersonService _personService;
    private readonly IMediaService _mediaService;

    public event Action? Saved;
    public event Action? Cancelled;

    public UpdateEventViewModel(
        IEventService eventService,
        ICompanyService companyService,
        IPersonService personService,
        IMediaService mediaService)
    {
        _eventService = eventService;
        _companyService = companyService;
        _personService = personService;
        _mediaService = mediaService;
    }

    // ----- Коллекции для UI -----

    public ObservableCollection<CompanySummary> Companies { get; } = new();
    public ObservableCollection<PersonSummary> Persons { get; } = new();
    public ObservableCollection<EventParticipantEditVM> Participants { get; } = new();
    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingAudio { get; } = new();

    // ----- Поля события -----

    [ObservableProperty] private int _eventId;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Название обязательно")]
    private string _title = string.Empty;

    [ObservableProperty] private DateTime? _date;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    // ----- Организаторы -----

    [ObservableProperty] private int? _organizerCompanyId;
    [ObservableProperty] private int? _organizerPersonId;

    // ----- Выбор нового участника в селекторах -----

    [ObservableProperty] private int? _selectedPersonId;
    [ObservableProperty] private int? _selectedCompanyId;

    // ----- Состояние -----

    [ObservableProperty] private bool _isBusy;

    // ----- Загрузка -----

    public async Task LoadAsync(int eventId)
    {
        _mediaService.DeleteTempFiles(TempFiles);
        TempFiles.Clear();
        ClearExistingMedia();
        Participants.Clear();

        if (Companies.Count == 0)
        {
            var companies = await _companyService.ListAsync(null);
            foreach (var c in companies) Companies.Add(c);
        }
        if (Persons.Count == 0)
        {
            var persons = await _personService.SearchAsync(string.Empty);
            foreach (var p in persons) Persons.Add(p);
        }

        var ev = await _eventService.GetByIdAsync(eventId);
        if (ev == null)
        {
            Notification.Show("Ошибка", $"Событие Id = {eventId} не найдено", NotificationType.Error);
            Cancelled?.Invoke();
            return;
        }

        EventId = ev.Id;
        Title = ev.Title;
        Date = ev.Date;
        Description = ev.Description ?? string.Empty;
        Notes = ev.Notes ?? string.Empty;
        OrganizerCompanyId = ev.OrganizerCompanyId;
        OrganizerPersonId = ev.OrganizerPersonId;

        foreach (var p in ev.Participants)
        {
            if (p.Person != null)
            {
                var display = p.Person.FullName
                    ?? p.Person.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
                    ?? $"Аноним #{p.Person.Id}";
                Participants.Add(EventParticipantEditVM.ForPerson(p.Person.Id, display));
            }
            else if (p.Company != null)
            {
                Participants.Add(EventParticipantEditVM.ForCompany(p.Company.Id, p.Company.Name));
            }
        }

        var mediaList = await _mediaService.GetMediaForOwnerAsync(MediaOwnerType.Event, eventId);
        foreach (var media in mediaList)
        {
            if (media.IsImage) ExistingImages.Add(media);
            else if (media.IsVideo) ExistingVideos.Add(media);
            else if (media.IsAudio) ExistingAudio.Add(media);
        }
    }

    private void ClearExistingMedia()
    {
        foreach (var m in ExistingImages) m.Dispose();
        foreach (var m in ExistingVideos) m.Dispose();
        foreach (var m in ExistingAudio) m.Dispose();
        ExistingImages.Clear(); ExistingVideos.Clear(); ExistingAudio.Clear();
    }

    // ----- Медиа -----

    public async Task HandleFilesDropAsync(string[] files)
    {
        var newFiles = await _mediaService.SaveToTempAsync(files);
        foreach (var file in newFiles) TempFiles.Add(file);
    }

    [RelayCommand]
    private void RemoveTempFile(MediaItemViewModel item)
    {
        if (item == null) return;
        TempFiles.Remove(item);
        _mediaService.DeleteTempFiles(new[] { item });
    }

    [RelayCommand]
    private async Task DeleteExistingMediaAsync(MediaItemViewModel media)
    {
        if (media == null || media.MediaId is null) return;
        if (!Notification.Confirm("Подтверждение", $"Удалить файл {media.FileName}?")) return;

        try
        {
            await _mediaService.RemoveLinkAsync(media.MediaId.Value, MediaOwnerType.Event, EventId);
            ExistingImages.Remove(media);
            ExistingVideos.Remove(media);
            ExistingAudio.Remove(media);
            media.Dispose();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка удаления: {ex.Message}", NotificationType.Error);
        }
    }

    // ----- Участники -----

    [RelayCommand]
    private void AddPersonParticipant()
    {
        if (SelectedPersonId == null) return;
        var person = Persons.FirstOrDefault(p => p.Id == SelectedPersonId.Value);
        if (person == null) return;
        if (Participants.Any(p => p.PersonId == person.Id)) return;

        Participants.Add(EventParticipantEditVM.ForPerson(person.Id, person.DisplayName));
        SelectedPersonId = null;
    }

    [RelayCommand]
    private void AddCompanyParticipant()
    {
        if (SelectedCompanyId == null) return;
        var company = Companies.FirstOrDefault(c => c.Id == SelectedCompanyId.Value);
        if (company == null) return;
        if (Participants.Any(p => p.CompanyId == company.Id)) return;

        Participants.Add(EventParticipantEditVM.ForCompany(company.Id, company.Name));
        SelectedCompanyId = null;
    }

    [RelayCommand]
    private void RemoveParticipant(EventParticipantEditVM item)
    {
        if (item == null) return;
        Participants.Remove(item);
    }

    // ----- Сохранение -----

    [RelayCommand]
    private async Task SaveAsync()
    {
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
                Participants.Select(p => p.ToInput()).ToList());

            await _eventService.UpdateAsync(EventId, input);

            if (TempFiles.Count > 0)
            {
                await _mediaService.CommitTempFilesAsync(TempFiles.ToList(), MediaOwnerType.Event, EventId);
                TempFiles.Clear();
            }

            Notification.Show("Успех", "Изменения сохранены", NotificationType.Success);
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка сохранения: {ex.Message}", NotificationType.Error);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel()
    {
        _mediaService.DeleteTempFiles(TempFiles);
        TempFiles.Clear();
        ClearExistingMedia();
        Cancelled?.Invoke();
    }
}