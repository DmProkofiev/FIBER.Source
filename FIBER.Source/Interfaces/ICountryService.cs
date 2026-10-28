using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

public interface ICountryService
{
    Task<List<Country>> GetActiveAsync();
}