using System.ComponentModel.DataAnnotations.Schema;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Models.Joins;

/// <summary>Прямая связь «организация ↔ личность» (человек работает/состоит в организации).</summary>
public class CompanyPerson
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    /// <summary>Роль человека в организации (сотрудник, владелец, партнёр и т.п.).</summary>
    public string Role { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    [NotMapped]
    public string RoleDisplay => string.IsNullOrWhiteSpace(Role) ? "Сотрудник" : Role;
}
