using System.Threading.Tasks;

namespace FIBER.Source.Interfaces;

/// <summary>
/// Абстракция над UI-диалогами. Позволяет VM быть независимыми от WPF
/// и покрываться юнит-тестами с mock-реализацией.
/// </summary>
public interface IDialogService
{
    Task ShowInfoAsync(string message, string title = "Информация");
    Task ShowWarningAsync(string message, string title = "Внимание");
    Task ShowErrorAsync(string message, string title = "Ошибка");

    /// <summary>true — пользователь выбрал «Да».</summary>
    Task<bool> ConfirmAsync(string message, string title = "Подтверждение");

    /// <summary>Возвращает путь или null при отмене.</summary>
    string? ShowSaveFileDialog(string filter, string defaultFileName, string title);

    /// <summary>Возвращает массив путей или null при отмене.</summary>
    string[]? ShowOpenFilesDialog(string filter, string title, bool multiselect = true);
}