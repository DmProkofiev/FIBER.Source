using FIBER.Source.Contracts;

namespace FIBER.Source.Interfaces;

public interface ISettingsDataService
{
    Task<SettingsData> LoadAsync();
}
