namespace FIBER.Source.Contracts.Inputs;

public record CompanyPersonEditInput(
    int? Id,
    int CompanyId,
    int PersonId,
    string PersonRole,
    string CompanyRole,
    string Notes = "");
