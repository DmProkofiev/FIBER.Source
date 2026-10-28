using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIBER.Source.Models;

/// <summary>Запись журнала действий приложения.</summary>
public class AuditLogEntry
{
    /// <summary>Первичный ключ.</summary>
    public int Id { get; set; }

    /// <summary>Время события.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Уровень.</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>Код действия.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Объект действия.</summary>
    public string ActionObject { get; set; } = "FIBER Source";

    /// <summary>Детали.</summary>
    public string? Details { get; set; }

    /// <summary>Человекочитаемое название действия для UI (не сохраняется в БД).</summary>
    [NotMapped]
    public string DisplayAction => Action switch
    {
        "Person.CreateIdentified" => "Создана личность",
        "Person.CreateAnonymous" => "Создана анонимная личность",
        "Person.Promote" => "Личность идентифицирована",
        "Person.Update" => "Личность изменена",
        "Person.UpdateAnonymous" => "Анонимная личность изменена",
        "Person.DeleteAlias" => "Удалён псевдоним",
        "Person.Delete" => "Личность удалена",
        "Company.Create" => "Создана организация",
        "Company.Update" => "Организация изменена",
        "Company.Delete" => "Организация удалена",
        "Event.Create" => "Создано событие",
        "Event.Update" => "Событие изменено",
        "Event.Delete" => "Событие удалено",
        "Media.Commit" => "Прикреплены медиафайлы",
        "Media.RemoveLink" => "Удалён медиафайл",
        "Media.RemoveAllForOwner" => "Удалены все медиафайлы владельца",
        "Media.ExtractFailed" => "Ошибка извлечения медиа",
        "Auth.Startup" => "Запуск приложения",
        "Auth.ChangePassword" => "Смена мастер-пароля",
        "Database.Export" => "Экспорт файла БД",
        "Storage.Maintenance" => "Обслуживание хранилища",
        _ => Action
    };
}