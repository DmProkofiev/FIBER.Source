using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.MediaManagement;
using System;
using System.Collections.Generic;
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
    private readonly IRelationService _relationService;
    private readonly IMediaService _mediaService;
    private readonly IMediaManagementService _mediaManagement;

    public event Action? Saved;
    public event Action? Cancelled;

    public UpdateEventViewModel(
        IEventService eventService,
        ICompanyService companyService,
        IPersonService personService,
        IRelationService relationService,
        IMediaService mediaService,
        IMediaManagementService mediaManagement)
    {
        _eventService = eventService;
        _companyService = companyService;
        _personService = personService;
        _relationService = relationService;
        _mediaService = mediaService;
        _mediaManagement = mediaManagement;
    }

    // ----- Коллекции для UI -----

    public ObservableCollection<CompanySummary> Companies { get; } = new();
    public ObservableCollection<PersonSummary> Persons { get; } = new();
    public ObservableCollection<EventSummary> AvailableEvents { get; } = new();
    public ObservableCollection<EventParticipantEditVM> Participants { get; } = new();
    public ObservableCollection<EventEventEditVM> EventEvents { get; } = new();
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
    [ObservableProperty] private int? _selectedTargetEventId;

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
            Companies.Add(new CompanySummary(0, "(не указан)", null));
            foreach (var c in companies) Companies.Add(c);
        }
        if (Persons.Count == 0)
        {
            var persons = await _personService.SearchAsync(string.Empty);
            Persons.Add(new PersonSummary(0, "(не указан)", IdentityStatus.Anonymous, null));
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

            // Точечный CRUD связей: запоминаем Id существующей записи участника,
            // чтобы при сохранении обновлять её на месте, а не пересоздавать.
            var last = Participants[^1];
            last.Id = p.Id;
            last.Role = p.Role;
            last.Notes = p.Notes;
        }

        // Справочник событий для связи «событие ↔ событие».
        AvailableEvents.Clear();
        foreach (var e in await _eventService.ListAsync(null))
            if (e.Id != eventId) AvailableEvents.Add(e);

        // Связи события с другими событиями.
        EventEvents.Clear();
        foreach (var ee in ev.EventEvents)
            EventEvents.Add(EventEventEditVM.FromModel(ee));

        await LoadExistingMediaAsync(eventId);
    }

    private OwnerMedia? _ownerMedia;

    // Загрузка медиа владельца в коллекции UI (изображения лениво подгружаются в память).
    private async Task LoadExistingMediaAsync(int ownerId)
    {
        _mediaManagement.ClearMedia(_ownerMedia ?? new OwnerMedia());
        _ownerMedia = await _mediaManagement.LoadOwnerMediaAsync(MediaOwnerType.Event, ownerId);

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
        EventEvents.Clear();
        AvailableEvents.Clear();
        SelectedTargetEventId = null;
    }

    // ----- Медиа -----

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
        await _mediaManagement.DeleteExistingMediaAsync(MediaOwnerType.Event, EventId, media);
        ExistingImages.Remove(media);
        ExistingVideos.Remove(media);
        ExistingAudio.Remove(media);
    }

    // ----- Участники -----

    [RelayCommand]
    private void AddPersonParticipant()
    {
        if (SelectedPersonId == null) return;
        var person = Persons.FirstOrDefault(p => p.Id == SelectedPersonId.Value);
        if (person == null) return;
        // Защита от дубля: такой участник уже в списке.
        if (Participants.Any(p => p.PersonId == person.Id))
        {
            Notification.Show("Внимание", "Этот участник уже добавлен", NotificationType.Warning);
            SelectedPersonId = null;
            return;
        }

        Participants.Add(EventParticipantEditVM.ForPerson(person.Id, person.DisplayName));
        SelectedPersonId = null;
    }

    [RelayCommand]
    private void AddCompanyParticipant()
    {
        if (SelectedCompanyId == null) return;
        var company = Companies.FirstOrDefault(c => c.Id == SelectedCompanyId.Value);
        if (company == null) return;
        // Защита от дубля: такой участник уже в списке.
        if (Participants.Any(p => p.CompanyId == company.Id))
        {
            Notification.Show("Внимание", "Эта организация уже добавлена как участник", NotificationType.Warning);
            SelectedCompanyId = null;
            return;
        }

        Participants.Add(EventParticipantEditVM.ForCompany(company.Id, company.Name));
        SelectedCompanyId = null;
    }

    [RelayCommand]
    private void RemoveParticipant(EventParticipantEditVM item)
    {
        if (item == null) return;
        Participants.Remove(item);
    }

    // ----- Связи «событие ↔ событие» -----

    [RelayCommand]
    private void AddEventEvent()
    {
        if (SelectedTargetEventId == null) return;

        var target = AvailableEvents.FirstOrDefault(e => e.Id == SelectedTargetEventId.Value);
        if (target == null) return;

        if (EventEvents.Any(ee => ee.TargetEventId == target.Id))
        {
            Notification.Show("Внимание", "Связь с этим событием уже добавлена", NotificationType.Warning);
            SelectedTargetEventId = null;
            return;
        }

        EventEvents.Add(new EventEventEditVM
        {
            TargetEventId = target.Id,
            TargetEventTitle = target.Title,
            RelationshipType = "Связано с"
        });
        SelectedTargetEventId = null;
    }

    [RelayCommand]
    private void RemoveEventEvent(EventEventEditVM eventEvent)
    {
        if (eventEvent == null) return;
        EventEvents.Remove(eventEvent);
    }

    private List<EventEventEditInput> BuildEventEventsInput() =>
        EventEvents
            .Where(ee => ee.TargetEventId.HasValue)
            .Select(ee => ee.ToInput())
            .ToList();

    // ----- Сохранение -----

    [RelayCommand]
    private async Task AddExistingMedia()
    {
        // Передаём уже добавленные MediaId из TempFiles, чтобы они отмечались галочкой и не дублировались
        var alreadyAdded = TempFiles.Where(m => m.MediaId.HasValue).Select(m => m.MediaId!.Value).ToList();
        var selected = await _mediaManagement.SelectExistingMediaAsync(MediaOwnerType.Event, EventId, alreadyAdded);
        if (selected.Count == 0) return;

        // Все элементы из SelectExistingMediaAsync имеют MediaId (они загружены из БД),
        // поэтому newMedia всегда пуст — упрощаем логику.
        var existingIds = selected
            .Where(m => m.MediaId.HasValue)
            .Select(m => m.MediaId!.Value)
            .ToList();

        // Уже привязанные к этому событию файлы пропускаются — о защите сообщаем пользователю.
        int skipped = 0;
        if (existingIds.Count > 0)
        {
            skipped = await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Event, EventId);
            await LoadExistingMediaAsync(EventId);
        }

        var added = existingIds.Count - skipped;
        if (skipped > 0)
        {
            Notification.Show("Внимание",
                $"{skipped} файл(ов) уже привязаны к этому событию и не добавлены повторно.",
                NotificationType.Warning);
        }
        if (added > 0)
        {
            Notification.Show("Успех", $"Привязано существующих медиа: {added}", NotificationType.Success);
        }
    }

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
                OrganizerCompanyId == 0 ? null : OrganizerCompanyId,
                OrganizerPersonId == 0 ? null : OrganizerPersonId,
                Participants.Select(p => p.ToInput()).ToList(),
                // Точечный CRUD связей: участники с Id обновляются на месте,
                // без Id — создаются, удалённые из списка — удаляются.
                ParticipantLinks: Participants.Select(p => p.ToEditInput()).ToList(),
                EventEvents: BuildEventEventsInput());

            await _eventService.UpdateAsync(EventId, input);

            if (TempFiles.Count > 0)
            {
                // Существующие медиа — привязать к владельцу
                var existingIds = TempFiles
                    .Where(m => m.MediaId.HasValue)
                    .Select(m => m.MediaId!.Value)
                    .ToList();
                if (existingIds.Count > 0)
                {
                    await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Event, EventId);
                }

                // Новые медиа — закоммитить в БД
                var newMedia = TempFiles.Where(m => !m.MediaId.HasValue).ToList();
                if (newMedia.Count > 0)
                {
                    await _mediaService.CommitTempFilesAsync(newMedia, MediaOwnerType.Event, EventId);
                }

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