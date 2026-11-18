using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIBER.Source.Models;

// Запись журнала действий приложения
public class AuditLogEntry
{
    //Первичный ключ
    public int Id { get; set; }

    // Время события
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Уровен
    public string Level { get; set; } = string.Empty;

    // Код действия
    public string Action { get; set; } = string.Empty;

    // Объект действия
    public string ActionObject { get; set; } = "FIBER Source";

    // Детали
    public string? Details { get; set; }

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
        "Auth.ChangePassword" => "Смена пароля",
        "Database.Export" => "Экспорт файла БД",
        "Storage.Maintenance" => "Обслуживание хранилища",
        _ => Action
    };
}