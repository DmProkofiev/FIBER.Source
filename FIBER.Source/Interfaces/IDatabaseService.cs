using System.Threading.Tasks;

namespace FIBER.Source.Interfaces;

/// <summary>
/// Управление единственным файлом БД fiber.db: открытие, смена пароля, экспорт/импорт.
/// </summary>
public interface IDatabaseService
{
    /// <summary>Абсолютный путь к файлу fiber.db.</summary>
    string DatabasePath { get; }

    /// <summary>Текущий мастер-пароль сессии.</summary>
    string CurrentPassword { get; }

    /// <summary>Проверяет, существует ли файл БД.</summary>
    bool DatabaseExists();

    /// <summary>Пытается открыть существующую БД с паролем; false при неверном пароле.</summary>
    Task<bool> TryUnlockAsync(string password);

    /// <summary>Создаёт новую БД с указанным паролем.</summary>
    Task CreateDatabaseAsync(string password);

    /// <summary>Смена мастер-пароля через PRAGMA rekey.</summary>
    Task ChangePasswordAsync(string oldPassword, string newPassword);

    /// <summary>Экспорт: копия fiber.db как есть (encrypted=true) или в открытом виде.</summary>
    Task ExportAsync(string targetFilePath, bool encrypted);
}