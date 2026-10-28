using System.Collections.Generic;
using System.Threading.Tasks;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

// Контракт сервиса событий
public interface IEventService
{
    // Создаёт событие
    Task<Event> CreateAsync(EventInput input);

    // Возвращает событие по Id
    Task<Event?> GetByIdAsync(int id);

    // Возвращает список событий с организатором
    Task<IReadOnlyList<EventSummary>> ListAsync(string? query = null);

    // Обновляет поля события
    Task UpdateAsync(int id, EventInput input);

    // Удаляет событие и его участников/медиа
    Task DeleteAsync(int id);
}