using System.ComponentModel.DataAnnotations.Schema;

namespace FIBER.Source.Models.Joins;

// связь организация человек
public class CompanyPerson
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string PersonRole { get; set; } = string.Empty;
    public string CompanyRole { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    [NotMapped]
    public string PersonRoleDisplay => string.IsNullOrWhiteSpace(PersonRole) ? "Сотрудник" : PersonRole;
    [NotMapped]
    public string CompanyRoleDisplay => string.IsNullOrWhiteSpace(CompanyRole) ? "Работодатель" : CompanyRole;
}
