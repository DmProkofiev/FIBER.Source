using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

// Редактируемая строка связи «организация ↔ личность» в форме редактирования.
public partial class CompanyPersonEditVM : ObservableObject
{
    // Id существующей записи CompanyPerson; null — новая связь.
    public int? Id { get; set; }

    [ObservableProperty] private int? _companyId;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private int? _personId;
    [ObservableProperty] private string _personName = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public CompanyPersonEditInput ToInput() => new(Id, CompanyId.GetValueOrDefault(), PersonId.GetValueOrDefault(), Role, Notes);

    public static CompanyPersonEditVM FromModel(CompanyPerson cp) => new()
    {
        Id = cp.Id,
        CompanyId = cp.CompanyId,
        CompanyName = cp.Company?.Name ?? $"Организация #{cp.CompanyId}",
        PersonId = cp.PersonId,
        PersonName = cp.Person?.FullName
            ?? cp.Person?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
            ?? $"Аноним #{cp.PersonId}",
        Role = cp.Role,
        Notes = cp.Notes
    };
}
