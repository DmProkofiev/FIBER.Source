using System.Threading.Tasks;
using FIBER.Source.Helpers;

namespace FIBER.Source.Interfaces;

/// <summary>
/// Абстракция показа пользовательских уведомлений и диалогов подтверждения.
/// Реализация vive в слое представления (Views) и инжектируется в ViewModels.
/// Бизнес-сервисы не должны зависеть от этого интерфейса.
/// </summary>
public interface INotificationService
{
    /// <summary>Показать уведомление с кнопкой ОК.</summary>
    void Show(string title, string message, NotificationType type = NotificationType.Info);

    /// <summary>Показать диалог подтверждения (Да/Нет). Возвращает true при подтверждении.</summary>
    bool Confirm(string title, string message);
}
