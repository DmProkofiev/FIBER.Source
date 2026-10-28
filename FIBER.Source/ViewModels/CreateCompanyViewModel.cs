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

// VM создания организации с медиа
public partial class CreateCompanyViewModel : ObservableValidator
{
    private readonly ICompanyService _companyService;
    private readonly IMediaService _mediaService;

    // Внедряет сервисы
    public CreateCompanyViewModel(ICompanyService companyService, IMediaService mediaService)
    {
        _companyService = companyService;
        _mediaService = mediaService;
    }

    public ObservableCollection<MediaItemViewModel> TempFiles { get; } = new();

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
        var newFiles = await _mediaService.SaveToTempAsync(files);
        foreach (var file in newFiles) TempFiles.Add(file);
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
                await _mediaService.CommitTempFilesAsync(TempFiles.ToList(), MediaOwnerType.Company, company.Id);
                TempFiles.Clear();
            }

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