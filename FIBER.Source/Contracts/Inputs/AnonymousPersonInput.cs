using System.Collections.Generic;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record AnonymousPersonInput(
    string PrimaryAlias,
    string Notes,
    Gender Gender = Gender.Unknown,
    string Job = "",
    string Phone = "",
    string City = "",
    string? CountryCode = null,
    string Car = "",
    string Mail = "",
    string SocialMedia = "",
    string Address = "",
    IReadOnlyList<PersonAliasInput>? Aliases = null,
    IReadOnlyList<PersonRelationshipEditInput>? Relationships = null);