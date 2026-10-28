using System;
using System.Collections.Generic;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record PersonInput(
    string LastName,
    string FirstName,
    string? SecondName,
    Gender Gender,
    DateTime? BirthDate,
    string Passport,
    string Job,
    string Phone,
    string City,
    string? CountryCode,
    string Car,
    string Mail,
    string SocialMedia,
    string Address,
    string Notes,
    IdentityStatus? Status = null,
    IReadOnlyList<PersonAliasInput>? Aliases = null);