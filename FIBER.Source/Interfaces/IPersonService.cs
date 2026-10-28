using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

// Контракт сервиса персон
public interface IPersonService
{
    Task<Person> CreateIdentifiedAsync(PersonInput input);
    Task<Person> CreateAnonymousAsync(AnonymousPersonInput input);
    Task<Person> PromoteAsync(int personId, PromotePersonInput input);

    Task<IReadOnlyList<PersonSummary>> SearchAsync(
        string query,
        string? strategyKey = null,
        bool? isIdentified = null);

    Task<IReadOnlyList<Person>> GetAllExceptAsync(int excludeId);
    Task<Person?> GetByIdAsync(int id);

    Task UpdateAsync(int id, PersonInput input);
    Task UpdateAnonymousAsync(int id, AnonymousPersonInput input);

    // Удалить один алиас. Если он primary и есть другие — один становится primary.
    // Если он единственный — InvalidOperationException.
    Task DeleteAliasAsync(int personId, int aliasId);

    Task DeleteAsync(int id);
}