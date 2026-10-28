using System.Collections.Generic;
using System.Threading.Tasks;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

// Контракт сервиса организаций
public interface ICompanyService
{
    // Создаёт организацию
    Task<Company> CreateAsync(CompanyInput input);

    // Возвращает организацию по Id
    Task<Company?> GetByIdAsync(int id);

    // Возвращает список организаций (с фильтром по подстроке имени)
    Task<IReadOnlyList<CompanySummary>> ListAsync(string? query);

    // Полный список организаций кроме указанной (для выбора цели связи в редакторе)
    Task<IReadOnlyList<Company>> GetAllExceptAsync(int excludeId);

    // Обновляет поля организации
    Task UpdateAsync(int id, CompanyInput input);

    // Удаляет организацию: self-ref, сотрудников, медиа владельца
    Task DeleteAsync(int id);
}