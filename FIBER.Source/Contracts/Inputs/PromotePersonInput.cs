using System;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record PromotePersonInput(
    string LastName,
    string FirstName,
    string? SecondName,
    Gender Gender,
    DateTime? BirthDate,
    string Passport);