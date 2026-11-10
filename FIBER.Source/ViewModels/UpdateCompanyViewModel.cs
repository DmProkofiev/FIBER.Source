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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class UpdateCompanyViewModel : ObservableValidator
{
    private readonly ICompanyService _companyService;
    private readonly IPersonService _personService;
    private readonly IEventService _eventService;
    private readonly IMediaService _mediaService;
    private readonly IMediaManagementService _mediaManagement;

    public event Action? Saved;
    public event Action? Cancelled;

    public UpdateCompanyViewModel(ICompanyService companyService, IPersonService personService, IEventService eventService, IMediaService mediaService, IMediaManagementService mediaManagement)
    {
        _companyService = companyService;
        _personService = personService;
        _eventService = eventService;
        _mediaService = mediaService;
        _mediaManagement = mediaManagement;
    }

    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingAudio { get; } = new();

    // Взаимодействия «компания → компания»: исходящие редактируются, обратные — для информации.
    public ObservableCollection<CompanyInteractionEditVM> Interactions { get; } = new();
    // Справочник организаций для ComboBox выбора цели новой связи.
    public ObservableCollection<Models.Company> AvailableCompanies { get; } = new();
    // Связи «организация ↔ персона».
    public ObservableCollection<CompanyPersonEditVM> CompanyPeople { get; } = new();
    // Справочник персон для связи с организацией.
    public ObservableCollection<Contracts.Summaries.PersonSummary> AvailablePersons { get; } = new();
    // Связи «организация ↔ событие».
    public ObservableCollection<EventParticipantEditVM> EventParticipations { get; } = new();
    // Справочник событий для связи с организацией.
    public ObservableCollection<EventSummary> AvailableEvents { get; } = new();

    [ObservableProperty] private int? _selectedTargetCompanyId;
    [ObservableProperty] private int? _selectedTargetPersonId;
    [ObservableProperty] private int? _selectedTargetEventId;

    [ObservableProperty] private int _companyId;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Название обязательно")]
    private string _name = string.Empty;

    [ObservableProperty] private string _registrationNumber = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _mail = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty] private bool _isBusy;

    public async Task LoadAsync(int companyId)
    {
        _mediaService.DeleteTempFiles(TempFiles);
        TempFiles.Clear();
        ClearExistingMedia();
        Interactions.Clear();
        SelectedTargetCompanyId = null;

        var company = await _companyService.GetByIdAsync(companyId);
        if (company == null)
        {
            Notification.Show("Ошибка", $"Организация Id = {companyId} не найдена", NotificationType.Error);
            Cancelled?.Invoke();
            return;
        }

        // Справочник организаций для выбора цели связи (исключаем саму организацию).
        AvailableCompanies.Clear();
        foreach (var c in await _companyService.GetAllExceptAsync(companyId))
            AvailableCompanies.Add(c);

        CompanyId = company.Id;
        Name = company.Name;
        RegistrationNumber = company.RegistrationNumber ?? string.Empty;
        Address = company.Address ?? string.Empty;
        Phone = company.Phone ?? string.Empty;
        Mail = company.Mail ?? string.Empty;
        Website = company.Website ?? string.Empty;
        Notes = company.Notes ?? string.Empty;

        // Взаимодействия: сначала исходящие (редактируемые), затем обратные (только просмотр).
        foreach (var ci in company.OutgoingInteractions)
            Interactions.Add(CompanyInteractionEditVM.FromModel(ci));
        foreach (var ci in company.IncomingInteractions)
            Interactions.Add(CompanyInteractionEditVM.FromReverse(ci));

        // Справочник персон для связи «организация ↔ персона».
        AvailablePersons.Clear();
        foreach (var p in await _personService.SearchAsync(string.Empty))
            AvailablePersons.Add(p);

        // Связи организации с персонами.
        CompanyPeople.Clear();
        foreach (var cp in company.CompanyPeople)
            CompanyPeople.Add(CompanyPersonEditVM.FromModel(cp));

        // Справочник событий для связи «организация ↔ событие».
        AvailableEvents.Clear();
        foreach (var e in await _eventService.ListAsync(null))
            AvailableEvents.Add(e);

        // Связи организации с событиями.
        EventParticipations.Clear();
        foreach (var ep in company.EventParticipations)
            EventParticipations.Add(EventParticipantEditVM.FromModel(ep));

        await LoadExistingMediaAsync(companyId);
    }

    private OwnerMedia? _ownerMedia;

    // Загрузка медиа владельца в коллекции UI (изображения лениво подгружаются в память).
    private async Task LoadExistingMediaAsync(int ownerId)
    {
        _mediaManagement.ClearMedia(_ownerMedia ?? new OwnerMedia());
        _ownerMedia = await _mediaManagement.LoadOwnerMediaAsync(MediaOwnerType.Company, ownerId);

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
        AvailablePersons.Clear();
        SelectedTargetPersonId = null;
        EventParticipations.Clear();
        AvailableEvents.Clear();
        SelectedTargetEventId = null;
    }

    // ----- Взаимодействия «компания → компания»: CRUD внутри редактора -----

    [RelayCommand]
    private void AddInteraction()
    {
        if (SelectedTargetCompanyId == null) return;

        var target = AvailableCompanies.FirstOrDefault(c => c.Id == SelectedTargetCompanyId.Value);
        if (target == null) return;

        // Защита от дубля на уровне UI: такая цель уже есть в списке связей.
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
            .Where(i => !i.IsReverseOnly && i.TargetCompanyId.HasValue)
            .Select(i => i.ToInput())
            .ToList();

    // ----- Связи «организация ↔ персона» -----

    [RelayCommand]
    private void AddPersonCompany()
    {
        if (SelectedTargetPersonId == null) return;

        var target = AvailablePersons.FirstOrDefault(p => p.Id == SelectedTargetPersonId.Value);
        if (target == null) return;

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
            CompanyId = CompanyId,  // Текущая организация
            Role = "Сотрудник"
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

    // ----- Связи «организация ↔ событие» -----

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
            CompanyId = CompanyId,  // Текущая организация
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
        await _mediaManagement.DeleteExistingMediaAsync(MediaOwnerType.Company, CompanyId, media);
        ExistingImages.Remove(media);
        ExistingVideos.Remove(media);
        ExistingAudio.Remove(media);
    }

    [RelayCommand]
    private async Task AddExistingMedia()
    {
        // Передаём уже добавленные MediaId из TempFiles, чтобы они отмечались галочкой и не дублировались
        var alreadyAdded = TempFiles.Where(m => m.MediaId.HasValue).Select(m => m.MediaId!.Value).ToList();
        var selected = await _mediaManagement.SelectExistingMediaAsync(MediaOwnerType.Company, CompanyId, alreadyAdded);
        if (selected.Count == 0) return;

        // Все элементы из SelectExistingMediaAsync имеют MediaId (они загружены из БД),
        // поэтому newMedia всегда пуст — упрощаем логику.
        var existingIds = selected
            .Where(m => m.MediaId.HasValue)
            .Select(m => m.MediaId!.Value)
            .ToList();

        // Уже привязанные к этой организации файлы пропускаются — о защите сообщаем пользователю.
        int skipped = 0;
        if (existingIds.Count > 0)
        {
            skipped = await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Company, CompanyId);
            await LoadExistingMediaAsync(CompanyId);
        }

        var added = existingIds.Count - skipped;
        if (skipped > 0)
        {
            Notification.Show("Внимание",
                $"{skipped} файл(ов) уже привязаны к этой организации и не добавлены повторно.",
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
            var input = new CompanyInput(
                Name.Trim(), RegistrationNumber.Trim(), Address.Trim(),
                Phone.Trim(), Mail.Trim(), Website.Trim(), Notes.Trim(),
                Interactions: BuildInteractionsInput(),
                CompanyPeople: BuildPersonCompaniesInput(),
                EventParticipations: BuildEventParticipationsInput());

            await _companyService.UpdateAsync(CompanyId, input);

            if (TempFiles.Count > 0)
            {
                // Существующие медиа — привязать к владельцу
                var existingIds = TempFiles
                    .Where(m => m.MediaId.HasValue)
                    .Select(m => m.MediaId!.Value)
                    .ToList();
                if (existingIds.Count > 0)
                {
                    await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Company, CompanyId);
                }

                // Новые медиа — закоммитить в БД
                var newMedia = TempFiles.Where(m => !m.MediaId.HasValue).ToList();
                if (newMedia.Count > 0)
                {
                    await _mediaService.CommitTempFilesAsync(newMedia, MediaOwnerType.Company, CompanyId);
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