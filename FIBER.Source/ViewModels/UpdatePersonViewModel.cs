using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class UpdatePersonViewModel : ObservableValidator
{
    private readonly IPersonService _personService;
    private readonly ICountryService _countryService;
    private readonly IMediaService _mediaService;

    public event Action? Saved;
    public event Action? Cancelled;

    public UpdatePersonViewModel(
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
    public ObservableCollection<MediaItemViewModel> ExistingImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingAudio { get; } = new();
    public ObservableCollection<PersonAliasEditVM> Aliases { get; } = new();

    [ObservableProperty] private int _personId;
    [ObservableProperty] private IdentityStatus _currentStatus;
    [ObservableProperty] private bool _isInPromoteMode;
    [ObservableProperty] private bool _isBusy;

    public bool IsIdentified => CurrentStatus >= IdentityStatus.Identified;
    public bool ShowIdentifiedFields => IsIdentified || IsInPromoteMode;
    public bool ShowAnonymousFields => !IsIdentified && !IsInPromoteMode;
    public bool ShowPromoteButton => !IsIdentified && !IsInPromoteMode;

    public string StatusDisplay => IsIdentified ? "Identified" : "Not identified";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "ФИО обязательно")]
    private string _fullName = string.Empty;

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
        _mediaService.DeleteTempFiles(TempFiles);
        TempFiles.Clear();
        ClearExistingMedia();
        Aliases.Clear();
        IsInPromoteMode = false;

        if (Countries.Count == 0)
        {
            var countries = await _countryService.GetActiveAsync();
            foreach (var c in countries) Countries.Add(c);
        }

        var person = await _personService.GetByIdAsync(personId);
        if (person == null)
        {
            Notification.Show("Ошибка", $"Личность Id = {personId} не найдена", NotificationType.Error);
            Cancelled?.Invoke();
            return;
        }

        PersonId = person.Id;
        CurrentStatus = person.Status;

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
            FullName = person.FullName ?? string.Empty;
            BirthDate = person.BirthDate;
            Passport = person.Passport ?? string.Empty;
        }
        else
        {
            PrimaryAlias = primary?.Value ?? string.Empty;
        }

        foreach (var a in person.Aliases.Where(a => !a.IsPrimary))
            Aliases.Add(PersonAliasEditVM.FromModel(a));

        var mediaList = await _mediaService.GetMediaForOwnerAsync(MediaOwnerType.Person, personId);
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

    [RelayCommand]
    private void AddAlias() => Aliases.Add(new PersonAliasEditVM { Type = AliasType.Alias });

    [RelayCommand]
    private void RemoveAlias(PersonAliasEditVM alias)
    {
        if (alias != null) Aliases.Remove(alias);
    }

    [RelayCommand]
    private void EnterPromoteMode()
    {
        if (IsIdentified) return;
        IsInPromoteMode = true;
        FullName = PrimaryAlias;
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

    [RelayCommand]
    private async Task DeleteExistingMediaAsync(MediaItemViewModel media)
    {
        if (media == null || media.MediaId is null) return;
        if (!Notification.Confirm("Подтверждение", $"Удалить файл {media.FileName}?")) return;

        try
        {
            await _mediaService.RemoveLinkAsync(media.MediaId.Value, MediaOwnerType.Person, PersonId);
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
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    Notification.Show("Ошибка", "Укажите ФИО для идентификации", NotificationType.Warning);
                    return;
                }

                await _personService.PromoteAsync(PersonId, new PromotePersonInput(
                    FullName: FullName.Trim(),
                    BirthDate: BirthDate,
                    Passport: Passport.Trim()));

                var personInput = new PersonInput(
                    FullName: FullName.Trim(), BirthDate: BirthDate, Passport: Passport.Trim(),
                    Job: Job.Trim(), Phone: Phone.Trim(), City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(), Mail: Mail.Trim(), SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(), Notes: Notes.Trim(),
                    Status: IdentityStatus.Identified,
                    Aliases: aliasesInput);

                await _personService.UpdateAsync(PersonId, personInput);
            }
            else if (IsIdentified)
            {
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    Notification.Show("Ошибка", "ФИО обязательно", NotificationType.Warning);
                    return;
                }

                var input = new PersonInput(
                    FullName: FullName.Trim(), BirthDate: BirthDate, Passport: Passport.Trim(),
                    Job: Job.Trim(), Phone: Phone.Trim(), City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(), Mail: Mail.Trim(), SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(), Notes: Notes.Trim(),
                    Status: IdentityStatus.Identified,
                    Aliases: aliasesInput);

                await _personService.UpdateAsync(PersonId, input);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(PrimaryAlias))
                {
                    Notification.Show("Ошибка", "Псевдоним обязателен", NotificationType.Warning);
                    return;
                }

                var input = new AnonymousPersonInput(
                    PrimaryAlias: PrimaryAlias.Trim(),
                    Notes: Notes.Trim(),
                    Job: Job.Trim(), Phone: Phone.Trim(), City: City.Trim(),
                    CountryCode: string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode,
                    Car: Car.Trim(), Mail: Mail.Trim(), SocialMedia: SocialMedia.Trim(),
                    Address: Address.Trim(),
                    Aliases: aliasesInput);

                await _personService.UpdateAnonymousAsync(PersonId, input);
            }

            if (TempFiles.Count > 0)
            {
                await _mediaService.CommitTempFilesAsync(TempFiles.ToList(), MediaOwnerType.Person, PersonId);
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