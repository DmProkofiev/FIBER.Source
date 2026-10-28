using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

public partial class RelationsViewModel : ObservableObject
{
    private readonly IRelationService _relationService;
    private readonly IPersonService _personService;
    private readonly ICompanyService _companyService;
    private readonly IEventService _eventService;
    private readonly MediaOwnerType _ownerType;
    private readonly int _ownerId;

    public RelationsViewModel(
        MediaOwnerType ownerType, int ownerId,
        IRelationService relationService, IPersonService personService,
        ICompanyService companyService, IEventService eventService)
    {
        _ownerType = ownerType; _ownerId = ownerId;
        _relationService = relationService;
        _personService = personService;
        _companyService = companyService;
        _eventService = eventService;
        KindOptions = new ObservableCollection<PersonRelationKindOption>(PersonRelationKinds.AllOptions);
        SelectedKind = KindOptions.FirstOrDefault();
        RoleOptions = new ObservableCollection<EventParticipantRoleOption>(EventParticipantRoles.AllOptions);
        SelectedRole = RoleOptions.FirstOrDefault();
    }

    public bool ShowPersonTabs => _ownerType == MediaOwnerType.Person;
    public bool ShowCompanyTabs => _ownerType == MediaOwnerType.Company;
    public bool ShowEventTabs => _ownerType == MediaOwnerType.Event;

    public ObservableCollection<EventParticipant> EventParticipations { get; } = new();
    public ObservableCollection<PersonRelationship> Relationships { get; } = new();
    public ObservableCollection<CompanyInteraction> CompanyInteractions { get; } = new();
    public ObservableCollection<EventParticipant> EventParticipants { get; } = new();

    public ObservableCollection<PersonSummary> AllPersons { get; } = new();
    public ObservableCollection<CompanySummary> AllCompanies { get; } = new();
    public ObservableCollection<EventSummary> AllEvents { get; } = new();

    public ObservableCollection<PersonRelationKindOption> KindOptions { get; }
    public ObservableCollection<EventParticipantRoleOption> RoleOptions { get; }

    [ObservableProperty] private PersonRelationKindOption? _selectedKind;
    [ObservableProperty] private EventParticipantRoleOption? _selectedRole;

    [ObservableProperty] private int? _selectedEventId;
    [ObservableProperty] private int? _participantPersonId;
    [ObservableProperty] private int? _participantCompanyId;

    [ObservableProperty] private int? _selectedRelatedPersonId;
    [ObservableProperty] private string _relationNotes = string.Empty;

    [ObservableProperty] private int? _selectedTargetCompanyId;
    [ObservableProperty] private string _interactionType = string.Empty;
    [ObservableProperty] private string _interactionNotes = string.Empty;

    [ObservableProperty] private bool _isBusy;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var persons = await _personService.SearchAsync(string.Empty);
            foreach (var p in persons) AllPersons.Add(p);

            var companies = await _companyService.ListAsync(null);
            foreach (var c in companies) AllCompanies.Add(c);

            var events = await _eventService.ListAsync();
            foreach (var e in events) AllEvents.Add(e);

            await ReloadAsync();
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    private async Task ReloadAsync()
    {
        EventParticipations.Clear(); Relationships.Clear();
        CompanyInteractions.Clear(); EventParticipants.Clear();

        if (ShowPersonTabs)
        {
            foreach (var ep in await _relationService.ListEventParticipantsAsync(_ownerId)) EventParticipations.Add(ep);
            foreach (var r in await _relationService.ListRelationshipsAsync(_ownerId)) Relationships.Add(r);
        }
        else if (ShowCompanyTabs)
        {
            foreach (var ci in await _relationService.ListCompanyInteractionsAsync(_ownerId)) CompanyInteractions.Add(ci);
        }
        else if (ShowEventTabs)
        {
            foreach (var ep in await _relationService.ListParticipantsByEventAsync(_ownerId)) EventParticipants.Add(ep);
        }
    }

    // ===== EventParticipant (персона -> событие) =====

    [RelayCommand]
    private async Task AddEventParticipationAsync()
    {
        if (SelectedEventId is null || SelectedRole is null)
        {
            Notification.Show("Внимание", "Выберите событие и роль", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddEventParticipantAsync(SelectedEventId.Value, _ownerId, null, SelectedRole.Value, string.Empty);
            await ReloadAsync();
            SelectedEventId = null;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveEventParticipationAsync(EventParticipant item)
    {
        if (item is null) return;
        IsBusy = true;
        try { await _relationService.RemoveEventParticipantAsync(item.Id); await ReloadAsync(); }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    // ===== EventParticipant (событие -> участники) =====

    [RelayCommand]
    private async Task AddParticipantToEventAsync()
    {
        if (SelectedRole is null)
        {
            Notification.Show("Внимание", "Выберите роль", NotificationType.Warning);
            return;
        }
        if (ParticipantPersonId is null && ParticipantCompanyId is null)
        {
            Notification.Show("Внимание", "Выберите персону или организацию", NotificationType.Warning);
            return;
        }
        if (ParticipantPersonId is not null && ParticipantCompanyId is not null)
        {
            Notification.Show("Внимание", "Участник должен быть человеком ИЛИ компанией", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddEventParticipantAsync(_ownerId, ParticipantPersonId, ParticipantCompanyId, SelectedRole.Value, string.Empty);
            await ReloadAsync();
            ParticipantPersonId = null;
            ParticipantCompanyId = null;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    // ===== PersonRelationship =====

    [RelayCommand]
    private async Task AddPersonRelationshipAsync()
    {
        if (SelectedRelatedPersonId is null || SelectedKind is null)
        {
            Notification.Show("Внимание", "Выберите связанную персону и тип связи", NotificationType.Warning);
            return;
        }
        if (SelectedRelatedPersonId.Value == _ownerId)
        {
            Notification.Show("Внимание", "Нельзя связать персону с самой собой", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddPersonRelationshipAsync(_ownerId, SelectedRelatedPersonId.Value, SelectedKind.Value, RelationNotes.Trim());
            await ReloadAsync();
            SelectedRelatedPersonId = null;
            RelationNotes = string.Empty;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemovePersonRelationshipAsync(PersonRelationship item)
    {
        if (item is null) return;
        IsBusy = true;
        try { await _relationService.RemovePersonRelationshipAsync(item.Id); await ReloadAsync(); }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    // ===== CompanyInteraction =====

    [RelayCommand]
    private async Task AddCompanyInteractionAsync()
    {
        if (SelectedTargetCompanyId is null)
        {
            Notification.Show("Внимание", "Выберите организацию", NotificationType.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(InteractionType))
        {
            Notification.Show("Внимание", "Укажите тип взаимодействия", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddCompanyInteractionAsync(_ownerId, SelectedTargetCompanyId.Value, InteractionType.Trim(), InteractionNotes.Trim());
            await ReloadAsync();
            SelectedTargetCompanyId = null;
            InteractionType = string.Empty;
            InteractionNotes = string.Empty;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveCompanyInteractionAsync(CompanyInteraction item)
    {
        if (item is null) return;
        IsBusy = true;
        try { await _relationService.RemoveCompanyInteractionAsync(item.SourceCompanyId, item.TargetCompanyId); await ReloadAsync(); }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }
}
