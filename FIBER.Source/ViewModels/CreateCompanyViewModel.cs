using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Contracts.Inputs;
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

// VM создания организации с медиа
public partial class CreateCompanyViewModel : ObservableValidator
{
    private readonly ICompanyService _companyService;
    private readonly IMediaService _mediaService;
    private readonly IMediaManagementService _mediaManagement;

    // Внедряет сервисы
    public CreateCompanyViewModel(ICompanyService companyService, IMediaService mediaService, IMediaManagementService mediaManagement)
    {
        _companyService = companyService;
        _mediaService = mediaService;
        _mediaManagement = mediaManagement;
    }

    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();

    /// <summary>Id только что созданной организации (для привязки существующих медиа).</summary>
    public int? CreatedOwnerId { get; private set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Название обязательно")]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _name = string.Empty;

    [ObservableProperty] private string _registrationNumber = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _mail = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isBusy;

    // Принимает drag-and-drop файлов
    public async Task HandleFilesDropAsync(string[] files)
    {
        await _mediaManagement.HandleFilesDropAsync(TempFiles, files);
    }

    // Удаляет временный файл
    [RelayCommand]
    private void RemoveTempFile(MediaItemViewModel item)
    {
        if (item == null) return;
        TempFiles.Remove(item);
        _mediaService.DeleteTempFiles(new[] { item });
    }

    // Создаёт организацию и коммитит медиа
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        CreatedOwnerId = null;
        ValidateAllProperties();
        if (HasErrors) return;

        IsBusy = true;
        try
        {
            var input = new CompanyInput(
                Name.Trim(), RegistrationNumber.Trim(), Address.Trim(),
                Phone.Trim(), Mail.Trim(), Website.Trim(), Notes.Trim());

            var company = await _companyService.CreateAsync(input);

            if (TempFiles.Count > 0)
            {
                // Существующие медиа (уже в БД) — привязать к владельцу
                var existingIds = TempFiles
                    .Where(m => m.MediaId.HasValue)
                    .Select(m => m.MediaId!.Value)
                    .ToList();
                if (existingIds.Count > 0)
                {
                    await _mediaManagement.LinkExistingMediaAsync(existingIds, MediaOwnerType.Company, company.Id);
                }

                // Новые медиа — закоммитить в БД
                var newMedia = TempFiles.Where(m => !m.MediaId.HasValue).ToList();
                if (newMedia.Count > 0)
                {
                    await _mediaService.CommitTempFilesAsync(newMedia, MediaOwnerType.Company, company.Id);
                }

                TempFiles.Clear();
            }

            CreatedOwnerId = company.Id;
            Notification.Show("Успех",
                $"Создана организация\nId = {company.Id}\nНазвание = {company.Name}",
                NotificationType.Success);
            ClearForm();
        }
        catch (Exception ex)
        {
            Notification.Show("Ошибка",
                $"Ошибка создания: {ex.Message}",
                NotificationType.Error);
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
    

    // Проверяет, можно ли запускать создание
    private bool CanCreate() => !IsBusy;

    // Очищает форму и освобождает временные файлы
    public void ClearForm()
    {
        Name = string.Empty;
        RegistrationNumber = string.Empty;
        Address = string.Empty;
        Phone = string.Empty;
        Mail = string.Empty;
        Website = string.Empty;
        Notes = string.Empty;
        _mediaService.DeleteTempFiles(TempFiles);
        TempFiles.Clear();
    }
}