using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FIBER.Source.ViewModels;

public partial class UpdateCompanyViewModel : ObservableValidator
{
    private readonly ICompanyService _companyService;
    private readonly IMediaService _mediaService;

    public event Action? Saved;
    public event Action? Cancelled;

    public UpdateCompanyViewModel(ICompanyService companyService, IMediaService mediaService)
    {
        _companyService = companyService;
        _mediaService = mediaService;
    }

    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingImages { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingVideos { get; } = new();
    public ObservableCollection<MediaItemViewModel> ExistingAudio { get; } = new();

    // Взаимодействия «компания → компания»: исходящие редактируются, обратные — для информации.
    public ObservableCollection<CompanyInteractionEditVM> Interactions { get; } = new();
    // Справочник организаций для ComboBox выбора цели новой связи.
    public ObservableCollection<Models.Company> AvailableCompanies { get; } = new();

    [ObservableProperty] private int? _selectedTargetCompanyId;

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

        var mediaList = await _mediaService.GetMediaForOwnerAsync(MediaOwnerType.Company, companyId);
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
            await _mediaService.RemoveLinkAsync(media.MediaId.Value, MediaOwnerType.Company, CompanyId);
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
        ValidateAllProperties();
        if (HasErrors) return;

        IsBusy = true;
        try
        {
            var input = new CompanyInput(
                Name.Trim(), RegistrationNumber.Trim(), Address.Trim(),
                Phone.Trim(), Mail.Trim(), Website.Trim(), Notes.Trim(),
                Interactions: BuildInteractionsInput());

            await _companyService.UpdateAsync(CompanyId, input);

            if (TempFiles.Count > 0)
            {
                await _mediaService.CommitTempFilesAsync(TempFiles.ToList(), MediaOwnerType.Company, CompanyId);
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