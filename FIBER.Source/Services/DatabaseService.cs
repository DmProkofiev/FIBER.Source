using System;
using System.IO;
using System.Threading.Tasks;
using FIBER.Source.Interfaces;
using Microsoft.Data.Sqlite;

namespace FIBER.Source.Services;

/// <summary>
/// Сервис управления единым зашифрованным файлом fiber.db (SQLCipher).
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
    /// <summary>Текущий мастер-пароль сессии.</summary>
    private string _password = string.Empty;

    /// <summary>Путь к файлу БД.</summary>
    public string DatabasePath { get; }

    /// <summary>Текущий мастер-пароль (только для внутренних сервисов).</summary>
    public string CurrentPassword => _password;

    /// <summary>Фиксирует путь к файлу fiber.db в каталоге приложения.</summary>
    public DatabaseService()
    {
        DatabasePath = Path.Combine(AppContext.BaseDirectory, "fiber.db");
    }

    /// <summary>Проверяет существование файла БД.</summary>
    public bool DatabaseExists() => File.Exists(DatabasePath);

    /// <summary>Пытается открыть БД с паролем; возвращает false при неверном пароле.</summary>
    public async Task<bool> TryUnlockAsync(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        if (!DatabaseExists()) return false;

        try
        {
            await using var conn = new SqliteConnection(BuildConnectionString(password));
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
            await cmd.ExecuteScalarAsync();
            _password = password;
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    /// <summary>Создаёт новую БД с указанным паролем.</summary>
    public async Task CreateDatabaseAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Пароль не может быть пустым", nameof(password));

        if (DatabaseExists())
            throw new InvalidOperationException("Файл БД уже существует");

        await using var conn = new SqliteConnection(BuildConnectionString(password));
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        await cmd.ExecuteNonQueryAsync();

        _password = password;
    }

    /// <summary>Смена мастер-пароля через PRAGMA rekey.</summary>
    public async Task ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword))
            throw new ArgumentException("Новый пароль не может быть пустым", nameof(newPassword));

        if (!await TryUnlockAsync(oldPassword))
            throw new InvalidOperationException("Неверный текущий пароль");

        await using var conn = new SqliteConnection(BuildConnectionString(oldPassword));
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA rekey = '{EscapeSql(newPassword)}';";
        await cmd.ExecuteNonQueryAsync();

        _password = newPassword;
    }

    /// <summary>Экспорт файла БД в выбранную папку.</summary>
    public async Task ExportAsync(string targetFilePath, bool encrypted)
    {
        if (string.IsNullOrWhiteSpace(targetFilePath))
            throw new ArgumentException("Путь экспорта не задан", nameof(targetFilePath));

        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (encrypted)
        {
            // WAL-журнал может содержать ещё не перенесённые в основной файл данные,
            // поэтому перед копированием принудительно выполняем checkpoint.
            await using (var conn = new SqliteConnection(BuildConnectionString(_password)))
            {
                await conn.OpenAsync();
                using var checkpoint = conn.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await checkpoint.ExecuteNonQueryAsync();
            }

            File.Copy(DatabasePath, targetFilePath, true);
        }
        else
        {
            await using var conn = new SqliteConnection(BuildConnectionString(_password));
            await conn.OpenAsync();

            using (var attach = conn.CreateCommand())
            {
                attach.CommandText = $"ATTACH DATABASE '{EscapeSql(targetFilePath)}' AS plaintext KEY '';";
                await attach.ExecuteNonQueryAsync();
            }

            using (var export = conn.CreateCommand())
            {
                export.CommandText = "SELECT sqlcipher_export('plaintext');";
                await export.ExecuteNonQueryAsync();
            }

            using (var detach = conn.CreateCommand())
            {
                detach.CommandText = "DETACH DATABASE plaintext;";
                await detach.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>Строит строку подключения с паролем.</summary>
    private string BuildConnectionString(string password)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Password = password,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };
        return csb.ToString();
    }

    /// <summary>Экранирует одиночные кавычки в SQL-строке.</summary>
    private static string EscapeSql(string s) => s.Replace("'", "''");
}