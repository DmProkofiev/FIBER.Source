using System.IO;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.Interfaces;
using Microsoft.Data.Sqlite;

namespace FIBER.Source.Services;

/// <summary>Управление ключом и согласованными снимками SQLCipher.</summary>
public sealed class DatabaseService : IDatabaseService
{
    private string _password = string.Empty;
    private readonly DatabaseAccessCoordinator _access;

    public DatabaseService(DatabaseAccessCoordinator access, string? databasePath = null)
    {
        _access = access;
        DatabasePath = databasePath ?? Path.Combine(AppContext.BaseDirectory, "fiber.db");
    }

    public string DatabasePath { get; }
    public string CurrentPassword => Volatile.Read(ref _password);

    public Task<bool> DatabaseExistsAsync() => Task.Run(() => File.Exists(DatabasePath));

    public async Task<bool> TryUnlockAsync(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        await BackgroundWork.SwitchToThreadPool();
        using var lease = await _access.AcquireAsync(exclusive: true);
        if (!File.Exists(DatabasePath)) return false;
        try
        {
            using var connection = Open(password);
            Verify(connection);
            Volatile.Write(ref _password, password);
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
        {
            return false;
        }
    }

    public async Task CreateDatabaseAsync(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        await BackgroundWork.SwitchToThreadPool();
        using var lease = await _access.AcquireAsync(exclusive: true);
        if (File.Exists(DatabasePath)) throw new InvalidOperationException("Файл БД уже существует");
        using var connection = Open(password, SqliteOpenMode.ReadWriteCreate);
        Execute(connection, "PRAGMA journal_mode=WAL;");
        Volatile.Write(ref _password, password);
    }

    public async Task ChangePasswordAsync(string oldPassword, string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        await BackgroundWork.SwitchToThreadPool();
        using var lease = await _access.AcquireAsync(exclusive: true);
        // Освобождаем все старые ключи из пула до rekey. Рабочих контекстов уже нет.
        SqliteConnection.ClearAllPools();
        try
        {
            using var connection = Open(oldPassword);
            Verify(connection);
            Execute(connection, $"PRAGMA rekey = '{Escape(newPassword)}';");
            Volatile.Write(ref _password, newPassword);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
        {
            throw new InvalidOperationException("Неверный текущий пароль", ex);
        }
        using var verification = Open(newPassword);
        Verify(verification);
    }

    public async Task ExportAsync(string targetFilePath, bool encrypted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);
        await BackgroundWork.SwitchToThreadPool();
        var target = Path.GetFullPath(targetFilePath);
        if (string.Equals(target, Path.GetFullPath(DatabasePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Нельзя экспортировать базу поверх рабочего файла.");

        using var lease = await _access.AcquireAsync(exclusive: true);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var staging = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            // ATTACH наследует флаги основного ReadWrite-соединения (без CREATE).
            await File.WriteAllBytesAsync(staging, []);
            // sqlcipher_export создаёт целостный снимок с учётом WAL и умеет менять ключ.
            using (var connection = Open(CurrentPassword))
            {
                using var attach = connection.CreateCommand();
                attach.CommandText = "ATTACH DATABASE $path AS backup KEY $key;";
                attach.Parameters.AddWithValue("$path", staging);
                attach.Parameters.AddWithValue("$key", encrypted ? CurrentPassword : string.Empty);
                attach.ExecuteNonQuery();
                try { Execute(connection, "SELECT sqlcipher_export('backup');"); }
                finally { Execute(connection, "DETACH DATABASE backup;"); }
            }
            File.Move(staging, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }

    private SqliteConnection Open(string password, SqliteOpenMode mode = SqliteOpenMode.ReadWrite)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, Password = password, Mode = mode,
            Cache = SqliteCacheMode.Private, Pooling = false, ForeignKeys = true
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void Verify(SqliteConnection connection) => Execute(connection, "SELECT count(*) FROM sqlite_master;");

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteScalar();
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
