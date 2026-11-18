using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.ViewModels;

public partial class CompanyPersonEditVM : ObservableObject
{
    public int? Id { get; set; }

    [ObservableProperty] private int? _companyId;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private int? _personId;
    [ObservableProperty] private string _personName = string.Empty;
    [ObservableProperty] private string _personRole = string.Empty;
    [ObservableProperty] private string _companyRole = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public CompanyPersonEditInput ToInput() => new(Id, CompanyId.GetValueOrDefault(), PersonId.GetValueOrDefault(), PersonRole, CompanyRole, Notes);

    public static CompanyPersonEditVM FromModel(CompanyPerson cp) => new()
    {
        Id = cp.Id,
        CompanyId = cp.CompanyId,
        CompanyName = cp.Company?.Name ?? $"Организация #{cp.CompanyId}",
        PersonId = cp.PersonId,
        PersonName = cp.Person?.FullName
            ?? cp.Person?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
            ?? $"Аноним #{cp.PersonId}",
        PersonRole = cp.PersonRole,
        CompanyRole = cp.CompanyRole,
        Notes = cp.Notes
    };
}
