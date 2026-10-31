using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using System;

namespace FIBER.Source.ViewModels;

// Главный VM: навигация между экранами CRUD, поиска и настроек
public partial class MainViewModel : ObservableObject
{
    private readonly MenuViewModel _menu;
    private readonly CreatePersonViewModel _createPerson;
    private readonly CreateCompanyViewModel _createCompany;
    private readonly CreateEventViewModel _createEvent;
    private readonly SearchViewModel _search;
    private readonly UpdatePersonViewModel _updatePerson;
    private readonly UpdateCompanyViewModel _updateCompany;
    private readonly UpdateEventViewModel _updateEvent;
    private readonly SettingsViewModel _settings;

    // Внедряет все VM и связывает события
    public MainViewModel(
        MenuViewModel menu,
        CreatePersonViewModel createPerson,
        CreateCompanyViewModel createCompany,
        CreateEventViewModel createEvent,
        SearchViewModel search,
        UpdatePersonViewModel updatePerson,
        UpdateCompanyViewModel updateCompany,
        UpdateEventViewModel updateEvent,
        SettingsViewModel settings)
    {
        _menu = menu;
        _createPerson = createPerson;
        _createCompany = createCompany;
        _createEvent = createEvent;
        _search = search;
        _updatePerson = updatePerson;
        _updateCompany = updateCompany;
        _updateEvent = updateEvent;
        _settings = settings;

        // Навигация из меню
        _menu.NavigationRequested += NavigateTo;

        // Запросы на редактирование из поиска (Person / Company / Event)
        _search.UpdatePersonRequested += OnUpdatePersonRequested;
        _search.UpdateCompanyRequested += OnUpdateCompanyRequested;
        _search.UpdateEventRequested += OnUpdateEventRequested;

        // Переходы по связям из карточек поиска — открываем карточку в режиме просмотра
        _search.NavigateToPersonRequested += OnNavigateToPerson;
        _search.NavigateToCompanyRequested += OnNavigateToCompany;
        _search.NavigateToEventRequested += OnNavigateToEvent;

        // Запросы на редактирование из настроек (Company / Event)
        _settings.UpdateCompanyRequested += OnSettingsCompanyRequested;
        _settings.UpdateEventRequested += OnSettingsEventRequested;

        // Возврат после сохранения/отмены
        _updatePerson.Saved += OnUpdateFinished;
        _updatePerson.Cancelled += OnUpdateFinished;
        _updateCompany.Saved += OnCompanyUpdateFinished;
        _updateCompany.Cancelled += OnCompanyUpdateFinished;
        _updateEvent.Saved += OnEventUpdateFinished;
        _updateEvent.Cancelled += OnEventUpdateFinished;

        _currentContent = _menu;
        _currentPanelKey = "Menu";
    }

    [ObservableProperty] private object _currentContent;
    [ObservableProperty] private string _currentPanelKey;

    // Возвращает пользователя в меню, очищая незавершённые формы
    [RelayCommand]
    private void ShowMenu()
    {
        if (CurrentPanelKey == "CreatePerson") _createPerson.ClearForm();
        if (CurrentPanelKey == "CreateCompany") _createCompany.ClearForm();
        if (CurrentPanelKey == "CreateEvent") _createEvent.ClearForm();

        _menu.CloseSubmenu();

        CurrentContent = _menu;
        CurrentPanelKey = "Menu";
    }

    // Обрабатывает запрос навигации из меню
    private void NavigateTo(string key)
    {
        switch (key)
        {
            case "CreatePerson":
                CurrentContent = _createPerson;
                CurrentPanelKey = "CreatePerson";
                break;
            case "CreateCompany":
                CurrentContent = _createCompany;
                CurrentPanelKey = "CreateCompany";
                break;
            case "CreateEvent":
                CurrentContent = _createEvent;
                CurrentPanelKey = "CreateEvent";
                _ = _createEvent.InitializeAsync();
                break;
            case "Search":
                CurrentContent = _search;
                CurrentPanelKey = "Search";
                _ = _search.InitializeAsync();
                break;
            case "Settings":
                CurrentContent = _settings;
                CurrentPanelKey = "Settings";
                _ = _settings.LoadDataAsync();
                break;
            case "Menu":
                ShowMenu();
                break;
        }
    }

    // Открывает редактирование персоны
    private async void OnUpdatePersonRequested(int personId)
    {
        try
        {
            await _updatePerson.LoadAsync(personId);
            CurrentContent = _updatePerson;
            CurrentPanelKey = "UpdatePerson";
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка",
                $"Ошибка открытия записи: {ex.Message}",
                NotificationType.Error);
        }
    }

    // Куда вернуться после сохранения/отмены редактирования организации/события.
    private bool _companyEditReturnsToSearch;
    private bool _eventEditReturnsToSearch;

    // Открывает редактирование организации
    private async void OnUpdateCompanyRequested(int companyId) =>
        await OpenCompanyAsync(companyId, returnToSearch: true);

    private async void OnSettingsCompanyRequested(int companyId) =>
        await OpenCompanyAsync(companyId, returnToSearch: false);

    private async Task OpenCompanyAsync(int companyId, bool returnToSearch)
    {
        _companyEditReturnsToSearch = returnToSearch;
        try
        {
            await _updateCompany.LoadAsync(companyId);
            CurrentContent = _updateCompany;
            CurrentPanelKey = "UpdateCompany";
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка",
                $"Ошибка открытия организации: {ex.Message}",
                NotificationType.Error);
        }
    }

    // Открывает редактирование события
    private async void OnUpdateEventRequested(int eventId) =>
        await OpenEventAsync(eventId, returnToSearch: true);

    private async void OnSettingsEventRequested(int eventId) =>
        await OpenEventAsync(eventId, returnToSearch: false);

    private async Task OpenEventAsync(int eventId, bool returnToSearch)
    {
        _eventEditReturnsToSearch = returnToSearch;
        try
        {
            await _updateEvent.LoadAsync(eventId);
            CurrentContent = _updateEvent;
            CurrentPanelKey = "UpdateEvent";
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка",
                $"Ошибка открытия события: {ex.Message}",
                NotificationType.Error);
        }
    }

    // ===== Переходы по связям: показываем целевой объект в режиме просмотра =====

    private async void OnNavigateToPerson(int personId)
    {
        ShowSearchView();
        await _search.ShowPersonAsync(personId);
    }

    private async void OnNavigateToCompany(int companyId)
    {
        ShowSearchView();
        await _search.ShowCompanyAsync(companyId);
    }

    private async void OnNavigateToEvent(int eventId)
    {
        ShowSearchView();
        await _search.ShowEventAsync(eventId);
    }

    // Открывает экран поиска без перезагрузки списков (чтобы не сбросить выбранную запись)
    private void ShowSearchView()
    {
        CurrentContent = _search;
        CurrentPanelKey = "Search";
        _menu.CloseSubmenu();
    }

    // Возврат в экран после сохранения/отмены (Person — экран поиска, Company/Event — туда, откуда открыли)
    private void OnUpdateFinished() => ShowSearch();

    private void OnCompanyUpdateFinished()
    {
        if (_companyEditReturnsToSearch) ShowSearch();
        else ShowSettings();
    }

    private void OnEventUpdateFinished()
    {
        if (_eventEditReturnsToSearch) ShowSearch();
        else ShowSettings();
    }

    // Переход на экран поиска (после сохранения/отмены Person)
    private void ShowSearch()
    {
        ShowSearchView();
        _ = _search.InitializeAsync();
    }

    // Переход на экран настроек (после сохранения/отмены Company/Event)
    private void ShowSettings()
    {
        CurrentContent = _settings;
        CurrentPanelKey = "Settings";
        _ = _settings.LoadDataAsync();
    }
}