using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FIBER.Source.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

/// <summary>
/// Универсальный синхронизатор связей "владелец ↔ цель".
/// Инкапсулирует повторяющийся паттерн CRUD связей, размазанный ранее
/// по PersonService, CompanyService и EventService (SRP/DRY).
///
/// Паттерн: DELETE (отсутствующие во входных данных) → UPDATE (существующие) → CREATE (новые).
/// </summary>
public sealed class LinkSynchronizer
{
    private readonly FiberSourceDbContext _context;

    public LinkSynchronizer(FiberSourceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Синхронизирует коллекцию связей владельца с целевыми объектами.
    /// </summary>
    /// <typeparam name="TLink">Тип записи связи в БД (класс сущности EF Core).</typeparam>
    /// <typeparam name="TInput">Входная модель связи.</typeparam>
    /// <param name="existingLinks">Текущие связи владельца из навигационного свойства контекста.</param>
    /// <param name="inputs">Входные данные связи (null — не трогать; пустой — удалить все).</param>
    /// <param name="ownerId">Идентификатор владельца (передаётся в фабрику создания связи).</param>
    /// <param name="getInputId">Извлечение Id существующей связи (null для новой).</param>
    /// <param name="getInputTargetId">Извлечение Id целевого объекта.</param>
    /// <param name="getInputRole">Извлечение роли/вида связи.</param>
    /// <param name="getInputNotes">Извлечение заметок.</param>
    /// <param name="setLinkFields">Заполнение полей связи (role, notes).</param>
    /// <param name="targetExists">Проверка существования целевого объекта по Id.</param>
    /// <param name="duplicateExists">Проверка дубля связи (ownerId, targetId).</param>
    /// <param name="createLink">Фабрика новой связи; получает targetId, ownerId доступен через замыкание.</param>
    public async Task SyncLinksAsync<TLink, TInput>(
        ICollection<TLink> existingLinks,
        IReadOnlyList<TInput>? inputs,
        int ownerId,
        Func<TInput, int?> getInputId,
        Func<TInput, int> getInputTargetId,
        Func<TInput, string?> getInputRole,
        Func<TInput, string?> getInputNotes,
        Action<TLink, string, string> setLinkFields,
        Func<int, Task<bool>> targetExists,
        Func<int, int, Task<bool>> duplicateExists,
        Func<int, TLink> createLink)
        where TLink : class
    {
        if (inputs is null) return; // null — не трогать; пустой список — удалить все

        // DELETE: связи, отсутствующие во входных данных.
        var inputIds = inputs
            .Where(i => getInputId(i) is not null)
            .Select(i => getInputId(i)!)
            .ToHashSet();
        foreach (var existing in existingLinks.ToList())
        {
            var existingId = GetLinkId(existing);
            if (inputIds.Contains(existingId)) continue;
            _context.Remove(existing);
        }

        // UPDATE + CREATE.
        foreach (var input in inputs)
        {
            var inputId = getInputId(input);
            var targetId = getInputTargetId(input);

            if (targetId <= 0) continue;
            if (!await targetExists(targetId))
                throw new InvalidOperationException($"Целевой объект Id={targetId} не найден");

            var link = inputId is not null
                ? existingLinks.FirstOrDefault(l => GetLinkId(l) == inputId.Value)
                : null;
            if (link is not null)
            {
                setLinkFields(link, getInputRole(input) ?? string.Empty,
                    getInputNotes(input) ?? string.Empty);
                continue;
            }

            // Защита от дубля.
            if (await duplicateExists(ownerId, targetId)) continue;
            if (inputs.Count(i => getInputTargetId(i) == targetId) > 1)
                throw new InvalidOperationException($"Целевой объект Id={targetId} указан во связях дважды");

            var newLink = createLink(targetId);
            setLinkFields(newLink, getInputRole(input) ?? string.Empty,
                getInputNotes(input) ?? string.Empty);
            _context.Add(newLink);
        }
    }

    private static int GetLinkId<TLink>(TLink link)
    {
        var prop = typeof(TLink).GetProperty("Id");
        return prop is not null ? (int)prop.GetValue(link)! : 0;
    }
}
