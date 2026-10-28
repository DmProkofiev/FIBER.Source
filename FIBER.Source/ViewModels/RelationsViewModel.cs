using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

/// <summary>VM окна связей: вкладки по типу владельца.</summary>
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
    }

    public bool ShowPersonTabs => _ownerType == MediaOwnerType.Person;
    public bool ShowCompanyTabs => _ownerType == MediaOwnerType.Company;
    public bool ShowEventTabs => _ownerType == MediaOwnerType.Event;

    // Коллекции «текущие связи»
    public ObservableCollection<Employment> Employments { get; } = new();
    public ObservableCollection<EventParticipant> EventParticipations { get; } = new();
    public ObservableCollection<PersonRelationship> Relationships { get; } = new();
    public ObservableCollection<CompanyInteraction> CompanyInteractions { get; } = new();
    public ObservableCollection<EventParticipant> EventParticipants { get; } = new();

    // Справочники для ComboBox
    public ObservableCollection<PersonSummary> AllPersons { get; } = new();
    public ObservableCollection<CompanySummary> AllCompanies { get; } = new();
    public ObservableCollection<EventSummary> AllEvents { get; } = new();

    // Employment
    [ObservableProperty] private int? _selectedCompanyId;
    [ObservableProperty] private string _position = string.Empty;
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _endDate;

    // EventParticipant (для персоны)
    [ObservableProperty] private int? _selectedEventId;

    // EventParticipant (для события — селекторы участников)
    [ObservableProperty] private int? _participantPersonId;
    [ObservableProperty] private int? _participantCompanyId;

    // PersonRelationship
    [ObservableProperty] private int? _selectedRelatedPersonId;
    [ObservableProperty] private string _relationType = string.Empty;
    [ObservableProperty] private string _relationNotes = string.Empty;

    // CompanyInteraction
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
        Employments.Clear(); EventParticipations.Clear(); Relationships.Clear();
        CompanyInteractions.Clear(); EventParticipants.Clear();

        if (ShowPersonTabs)
        {
            foreach (var e in await _relationService.ListEmploymentsAsync(_ownerId)) Employments.Add(e);
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

    // ===== Employment =====

    [RelayCommand]
    private async Task AddEmploymentAsync()
    {
        if (SelectedCompanyId is null)
        {
            Notification.Show("Внимание", "Выберите организацию", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddEmploymentAsync(_ownerId, SelectedCompanyId.Value, Position.Trim(), StartDate, EndDate);
            await ReloadAsync();
            Position = string.Empty; StartDate = null; EndDate = null; SelectedCompanyId = null;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveEmploymentAsync(Employment item)
    {
        if (item is null) return;
        IsBusy = true;
        try { await _relationService.RemoveEmploymentAsync(item.PersonId, item.CompanyId); await ReloadAsync(); }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    // ===== EventParticipant (персона → событие) =====

    [RelayCommand]
    private async Task AddEventParticipationAsync()
    {
        if (SelectedEventId is null)
        {
            Notification.Show("Внимание", "Выберите событие", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddEventParticipantAsync(SelectedEventId.Value, _ownerId, null);
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

    // ===== EventParticipant (событие → участники) =====

    [RelayCommand]
    private async Task AddParticipantPersonToEventAsync()
    {
        if (ParticipantPersonId is null)
        {
            Notification.Show("Внимание", "Выберите человека", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddEventParticipantAsync(_ownerId, ParticipantPersonId.Value, null);
            await ReloadAsync();
            ParticipantPersonId = null;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddParticipantCompanyToEventAsync()
    {
        if (ParticipantCompanyId is null)
        {
            Notification.Show("Внимание", "Выберите организацию", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddEventParticipantAsync(_ownerId, null, ParticipantCompanyId.Value);
            await ReloadAsync();
            ParticipantCompanyId = null;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    // ===== PersonRelationship =====

    [RelayCommand]
    private async Task AddPersonRelationshipAsync()
    {
        if (SelectedRelatedPersonId is null)
        {
            Notification.Show("Внимание", "Выберите персону", NotificationType.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _relationService.AddPersonRelationshipAsync(_ownerId, SelectedRelatedPersonId.Value, RelationType.Trim(), RelationNotes.Trim());
            await ReloadAsync();
            RelationType = string.Empty; RelationNotes = string.Empty; SelectedRelatedPersonId = null;
        }
        catch (Exception ex) { Notification.Show("Ошибка", ex.Message, NotificationType.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemovePersonRelationshipAsync(PersonRelationship item)
    {
        if (item is null) return;
        IsBusy = true;
        try { await _relationService.RemovePersonRelationshipAsync(item.PersonId, item.RelatedPersonId); await ReloadAsync(); }
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
        IsBusy = true;
        try
        {
            await _relationService.AddCompanyInteractionAsync(_ownerId, SelectedTargetCompanyId.Value, InteractionType.Trim(), InteractionNotes.Trim());
            await ReloadAsync();
            InteractionType = string.Empty; InteractionNotes = string.Empty; SelectedTargetCompanyId = null;
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