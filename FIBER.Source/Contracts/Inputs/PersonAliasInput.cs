using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;
public record PersonAliasInput(
    int? Id,
    string Value,
    AliasType Type,
    bool IsPrimary);