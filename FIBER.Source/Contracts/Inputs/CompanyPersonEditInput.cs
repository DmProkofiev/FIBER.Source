using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

public record CompanyPersonEditInput(
    int? Id,
    int CompanyId,
    int PersonId,
    string Role,
    string Notes = "");
