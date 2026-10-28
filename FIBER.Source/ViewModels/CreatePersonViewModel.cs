using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class CreatePersonViewModel : ObservableValidator
{
    private readonly IPersonService _personService;
    private readonly ICountryService _countryService;
    private readonly IMediaService _mediaService;

    public CreatePersonViewModel(
        IPersonService personService,
        ICountryService countryService,
        IMediaService mediaService)
    {
        _personService = personService;
        _countryService = countryService;
        _mediaService = mediaService;
    }

    public ObservableCollection<Country> Countries { get; } = new();
    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdentifiedMode))]
    private bool _isAnonymousMode;

    public bool IsIdentifiedMode => !IsAnonymousMode;

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
    }

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

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        IsBusy = true;
        try
        {
            int personId;

            if (IsAnonymousMode)
            {
                if (string.IsNullOrWhiteSpace(PrimaryAlias))
                {
                    Notification.Show("Ошибка", "Укажите хотя бы один псевдоним", NotificationType.Warning);
                    return;
                }

                var input = new AnonymousPersonInput(
                    PrimaryAlias: PrimaryAlias.Trim(),
                    Notes: Notes.Trim(),
                    Job: Job.Trim(),
                    Phone: Phone.Trim(),
                    City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(),
                    Mail: Mail.Trim(),
                    SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim());

                var person = await _personService.CreateAnonymousAsync(input);
                personId = person.Id;
                Notification.Show("Успех",
                    $"Создана неидентифицированная личность\nId = {person.Id}\nПсевдоним = {PrimaryAlias}",
                    NotificationType.Success);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    Notification.Show("Ошибка", "Для идентифицированной личности ФИО обязательно", NotificationType.Warning);
                    return;
                }

                var input = new PersonInput(
                    FullName: FullName.Trim(),
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
                    Status: IdentityStatus.Identified);

                var person = await _personService.CreateIdentifiedAsync(input);
                personId = person.Id;
                Notification.Show("Успех",
                    $"Создана идентифицированная личность\nId = {person.Id}\nФИО = {FullName}",
                    NotificationType.Success);
            }

            if (TempFiles.Count > 0)
            {
                await _mediaService.CommitTempFilesAsync(TempFiles.ToList(), MediaOwnerType.Person, personId);
                TempFiles.Clear();
            }

            ClearForm();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка", $"Ошибка создания: {ex.Message}", NotificationType.Error);
        }
        finally { IsBusy = false; }
    }

    private bool CanCreate() => !IsBusy;

    public void ClearForm()
    {
        IsAnonymousMode = false;
        FullName = string.Empty;
        BirthDate = null;
        Passport = string.Empty;
        PrimaryAlias = string.Empty;
        Job = string.Empty;
        Phone = string.Empty;
        City = string.Empty;
        CountryCode = null;
        Car = string.Empty;
        Mail = string.Empty;
        SocialMedia = string.Empty;
        Address = string.Empty;
        Notes = string.Empty;

        _mediaService.DeleteTempFiles(TempFiles);
        TempFiles.Clear();
    }
}