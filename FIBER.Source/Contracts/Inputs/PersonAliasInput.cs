using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

// Входные данные одного алиаса при обновлении/создании персоны.
public record PersonAliasInput(
    int? Id,
    string Value,
    AliasType Type,
    bool IsPrimary);