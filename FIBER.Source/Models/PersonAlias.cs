using System;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Models;

// Имя, под которым известна личность
public class PersonAlias
{
    public int Id { get; set; }
    public int PersonId { get; set; }

    public string Value { get; set; } = string.Empty;
    public AliasType Type { get; set; } = AliasType.Other;
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Person Person { get; set; } = null!;
}