using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NewPerson = FIBER.Source.Models.Person;
using OldPerson = Person;

namespace FIBER.Source.Migration;

internal static class Program
{
    private const string DefaultSourceDb = "Data/PeopleRegister.db";
    private const string DefaultTargetDb = "fiber.db";
    private const string DefaultPassword = "";
    private const string DefaultMediaDir = "Data/MediaInformation";

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : string.Empty;

        if (command == "migrate")
            return RunMigrate(args.Skip(1).ToArray());

        if (command == "--help" || command == "-h" || command == "help")
        {
            PrintHelp();
            return 0;
        }

        if (string.IsNullOrEmpty(command))
        {
            PrintHelp();
            return 0;
        }

        Console.Error.WriteLine($"Неизвестная команда: {command}");
        PrintHelp();
        return 2;
    }

    private static int RunMigrate(string[] args)
    {
        var source = DefaultSourceDb;
        var target = DefaultTargetDb;
        var password = DefaultPassword;
        var mediaDir = DefaultMediaDir;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source" when i + 1 < args.Length:
                    source = args[++i];
                    break;
                case "--target" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                case "--password" when i + 1 < args.Length:
                    password = args[++i];
                    break;
                case "--media-dir" when i + 1 < args.Length:
                    mediaDir = args[++i];
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
                default:
                    Console.Error.WriteLine($"Неизвестный аргумент: {args[i]}");
                    return 2;
            }
        }

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"Исходная база не найдена: {source}");
            return 1;
        }

        Console.WriteLine($"Исходная БД:    {source}");
        Console.WriteLine($"Целевая БД:     {target}");
        Console.WriteLine($"Папка медиа:    {mediaDir}");
        Console.WriteLine($"Пароль SQLCipher: {(password.Length > 0 ? "задан" : "не задан (открытая БД)")}");
        Console.WriteLine();

        try
        {
            var services = BuildServices(target, password);
            using var scope = services.CreateScope();
            var migrator = scope.ServiceProvider.GetRequiredService<PersonDataMigrator>();
            var stats = migrator.Migrate(source, mediaDir).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("=== ИТОГИ МИГРАЦИИ ===");
            Console.WriteLine($"Обработано записей Person: {stats.PersonsRead}");
            Console.WriteLine($"Перенесено в новую БД:     {stats.PersonsInserted}");
            Console.WriteLine($"Пропущено (ошибки):        {stats.PersonsSkipped}");
            Console.WriteLine($"Медиафайлов найдено:       {stats.MediaFilesFound}");
            Console.WriteLine($"Медиафайлов обработано:    {stats.MediaFilesProcessed}");
            Console.WriteLine($"Ошибок медиа:              {stats.MediaErrors}");
            Console.WriteLine();
            Console.WriteLine("Миграция завершена.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка миграции: {ex.Message}");
            return 1;
        }
    }

    private static ServiceProvider BuildServices(string targetDb, string password)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        services.AddDbContextFactory<FiberSourceDbContext>((sp, options) =>
        {
            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = targetDb,
                Password = password,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private
            };
            options.UseSqlite(csb.ToString());
        });

        services.AddTransient<PersonDataMigrator>();

        return services.BuildServiceProvider();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            "Использование:\n" +
            "  FIBER.Migration migrate [опции]\n" +
            "\n" +
            "Опции:\n" +
            "  --source <path>   Путь к старой PeopleRegister.db (по умолчанию Data/PeopleRegister.db)\n" +
            "  --target <path>   Путь к новой fiber.db (по умолчанию fiber.db)\n" +
            "  --password <pw>   Пароль SQLCipher для целевой БД\n" +
            "  --media-dir <path> Папка со старыми медиафайлами (по умолчанию Data/MediaInformation)\n" +
            "  --help, -h        Эта справка\n" +
            "\n" +
            "Пример:\n" +
            "  FIBER.Migration migrate --source Data/PeopleRegister.db --target fiber.db --password mypass");
    }
}

public sealed class MigrationStats
{
    public int PersonsRead { get; set; }
    public int PersonsInserted { get; set; }
    public int PersonsSkipped { get; set; }
    public int MediaFilesFound { get; set; }
    public int MediaFilesProcessed { get; set; }
    public int MediaErrors { get; set; }
    public List<string> Errors { get; } = new();
}

public sealed class PersonDataMigrator
{
    private readonly IDbContextFactory<FiberSourceDbContext> _targetFactory;
    private readonly ILogger<PersonDataMigrator> _logger;

    public PersonDataMigrator(
        IDbContextFactory<FiberSourceDbContext> targetFactory,
        ILogger<PersonDataMigrator> logger)
    {
        _targetFactory = targetFactory;
        _logger = logger;
    }

    public async Task<MigrationStats> Migrate(string sourceDbPath, string mediaDir)
    {
        var stats = new MigrationStats();

        // 1. Читаем старые Person
        var oldPeople = await ReadOldPeopleAsync(sourceDbPath, stats);
        stats.PersonsRead = oldPeople.Count;
        _logger.LogInformation("Прочитано старых записей Person: {Count}", oldPeople.Count);

        // 2. Применяем миграции EF Core к целевой БД (создаёт таблицы, если их нет)
        await using var targetContext = await _targetFactory.CreateDbContextAsync();
        await targetContext.Database.MigrateAsync();
        _logger.LogInformation("Миграции EF Core применены к целевой БД");

        // Загружаем допустимые коды стран из справочника (FK CountryCode -> Countries.Code)
        var validCountryCodes = await targetContext.Countries
            .Select(c => c.Code)
            .ToHashSetAsync();
        _logger.LogInformation("Загружено кодов стран: {Count}", validCountryCodes.Count);

        int inserted = 0;
        int skipped = 0;

        // Сохраняем соответствие старого Id -> новый объект, чтобы после SaveChanges узнать новые Id
        var oldToNew = new Dictionary<int, NewPerson>();

        foreach (var old in oldPeople)
        {
            try
            {
                var newPerson = MapOldToNew(old, validCountryCodes);

                // Валидация: если нет никаких идентифицирующих данных — пропускаем
                if (string.IsNullOrWhiteSpace(newPerson.LastName) &&
                    string.IsNullOrWhiteSpace(newPerson.FirstName) &&
                    string.IsNullOrWhiteSpace(newPerson.Passport) &&
                    string.IsNullOrWhiteSpace(newPerson.Phone))
                {
                    stats.PersonsSkipped++;
                    stats.Errors.Add($"Person Id={old.Id}: нет идентифицирующих данных, пропущен");
                    _logger.LogWarning("Person Id={Id} пропущен: нет идентифицирующих данных", old.Id);
                    skipped++;
                    continue;
                }

                targetContext.People.Add(newPerson);
                oldToNew[old.Id] = newPerson;
                inserted++;
            }
            catch (Exception ex)
            {
                stats.PersonsSkipped++;
                stats.Errors.Add($"Person Id={old.Id}: {ex.Message}");
                _logger.LogError(ex, "Person Id={Id} не перенесён", old.Id);
                skipped++;
            }
        }

        await targetContext.SaveChangesAsync();
        stats.PersonsInserted = inserted;
        _logger.LogInformation("Вставлено новых Person: {Count}", inserted);
        _logger.LogInformation("Пропущено Person: {Count}", skipped);

        // 3. Обрабатываем медиафайлы для каждой персоны (MediaInformation — список имён через запятую)
        ProcessMediaForPersons(oldPeople, oldToNew, mediaDir, targetContext, stats);

        await targetContext.SaveChangesAsync();
        _logger.LogInformation("Медиа-ссылки сохранены в БД");

        return stats;
    }

    private static async Task<List<OldPerson>> ReadOldPeopleAsync(string sourceDbPath, MigrationStats stats)
    {
        // OldPerson = Person из ConsoleApp/Person.cs (старая модель предыдущего проекта)
        var people = new List<OldPerson>();

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };

        await using var conn = new SqliteConnection(csb.ToString());
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, FullName, BirthDate, Job, Phone, Passport, City, Country,
                   Car, Mail, SocialMedia, MediaInformation, Address, Notes
            FROM PeopleInformation";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            try
            {
                people.Add(new OldPerson
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                    BirthDate = reader.IsDBNull(reader.GetOrdinal("BirthDate")) ? string.Empty : reader.GetString(reader.GetOrdinal("BirthDate")),
                    Job = reader.IsDBNull(reader.GetOrdinal("Job")) ? string.Empty : reader.GetString(reader.GetOrdinal("Job")),
                    Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("Phone")),
                    Passport = reader.IsDBNull(reader.GetOrdinal("Passport")) ? string.Empty : reader.GetString(reader.GetOrdinal("Passport")),
                    City = reader.IsDBNull(reader.GetOrdinal("City")) ? string.Empty : reader.GetString(reader.GetOrdinal("City")),
                    Country = reader.IsDBNull(reader.GetOrdinal("Country")) ? string.Empty : reader.GetString(reader.GetOrdinal("Country")),
                    Car = reader.IsDBNull(reader.GetOrdinal("Car")) ? string.Empty : reader.GetString(reader.GetOrdinal("Car")),
                    Mail = reader.IsDBNull(reader.GetOrdinal("Mail")) ? string.Empty : reader.GetString(reader.GetOrdinal("Mail")),
                    SocialMedia = reader.IsDBNull(reader.GetOrdinal("SocialMedia")) ? string.Empty : reader.GetString(reader.GetOrdinal("SocialMedia")),
                    MediaInformation = reader.IsDBNull(reader.GetOrdinal("MediaInformation")) ? string.Empty : reader.GetString(reader.GetOrdinal("MediaInformation")),
                    Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? string.Empty : reader.GetString(reader.GetOrdinal("Address")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? string.Empty : reader.GetString(reader.GetOrdinal("Notes")),
                });
            }
            catch (Exception ex)
            {
                stats.PersonsSkipped++;
                stats.Errors.Add($"Чтение Person: {ex.Message}");
            }
        }

        return people;
    }

    private static NewPerson MapOldToNew(OldPerson old, HashSet<string> validCountryCodes)
    {
        // old — ConsoleApp/Person.cs (старая модель)
        // Возвращаем FIBER.Source.Models.Person (новая модель)
        var country = string.IsNullOrWhiteSpace(old.Country) ? null : old.Country.Trim();
        // FK: CountryCode должен существовать в справочнике Countries, иначе — null
        if (country is not null && !validCountryCodes.Contains(country))
        {
            country = null;
        }

        var newPerson = new NewPerson
        {
            Status = IdentityStatus.Anonymous,
            LastName = null,
            FirstName = null,
            SecondName = null,
            Gender = Gender.Unknown,
            BirthDate = null,
            Passport = string.IsNullOrWhiteSpace(old.Passport) ? null : old.Passport.Trim(),
            Job = string.IsNullOrWhiteSpace(old.Job) ? null : old.Job.Trim(),
            Phone = string.IsNullOrWhiteSpace(old.Phone) ? null : old.Phone.Trim(),
            City = string.IsNullOrWhiteSpace(old.City) ? null : old.City.Trim(),
            CountryCode = country,
            Car = string.IsNullOrWhiteSpace(old.Car) ? null : old.Car.Trim(),
            Mail = string.IsNullOrWhiteSpace(old.Mail) ? null : old.Mail.Trim(),
            SocialMedia = string.IsNullOrWhiteSpace(old.SocialMedia) ? null : old.SocialMedia.Trim(),
            Address = string.IsNullOrWhiteSpace(old.Address) ? null : old.Address.Trim(),
            Notes = string.IsNullOrWhiteSpace(old.Notes) ? null : old.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            MergedIntoPersonId = null,
        };

        // Разбиваем FullName на LastName / FirstName / SecondName
        if (!string.IsNullOrWhiteSpace(old.FullName))
        {
            var parts = old.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1) newPerson.LastName = parts[0];
            if (parts.Length >= 2) newPerson.FirstName = parts[1];
            if (parts.Length >= 3) newPerson.SecondName = string.Join(" ", parts.Skip(2));

            if (!string.IsNullOrEmpty(newPerson.LastName) && !string.IsNullOrEmpty(newPerson.FirstName))
                newPerson.Status = IdentityStatus.Identified;
            else
            {
                // Для анонима все поля ФИО должны быть NULL (CHECK constraint)
                newPerson.LastName = null;
                newPerson.FirstName = null;
                newPerson.SecondName = null;
            }
        }

        // Парсим BirthDate из строки
        if (!string.IsNullOrWhiteSpace(old.BirthDate) &&
            DateTime.TryParse(old.BirthDate, out var birthDate))
        {
            newPerson.BirthDate = birthDate;
        }

        return newPerson;
    }

    private void ProcessMediaForPersons(
        List<OldPerson> oldPeople,
        Dictionary<int, NewPerson> oldToNew,
        string mediaDir,
        FiberSourceDbContext targetContext,
        MigrationStats stats)
    {
        if (!Directory.Exists(mediaDir))
        {
            _logger.LogWarning("Папка медиа не найдена: {Dir}", mediaDir);
            stats.Errors.Add($"Папка медиа не найдена: {mediaDir}");
            return;
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
            ".mp4", ".avi", ".wmv",
            ".mp3", ".wav", ".wma", ".m4a", ".mov"
        };

        // Существующие медиа в БД по ContentHash (дедупликация BLOB)
        var existingMediaByHash = targetContext.MediaAttachments
            .Select(m => new { m.ContentHash, m.Id })
            .ToDictionary(m => m.ContentHash, m => m.Id);

        // Локальный кэш медиа, созданных в этом запуске (хэш -> объект)
        var newMediaByHash = new Dictionary<string, MediaAttachment>();

        // Собираем все задачи: (старая персони, новый объект, имя файла, путь, хэш, данные, mime)
        var mediaTasks = new List<(OldPerson Old, NewPerson New, string FileName, string FilePath, string Hash, byte[] Data, string MimeType)>();

        int found = 0;

        foreach (var old in oldPeople)
        {
            if (!oldToNew.TryGetValue(old.Id, out var newPerson))
                continue;

            var mediaNames = ParseMediaInformation(old.MediaInformation);
            if (mediaNames.Count == 0)
                continue;

            foreach (var fileName in mediaNames)
            {
                found++;

                var filePath = Path.Combine(mediaDir, fileName);
                if (!File.Exists(filePath))
                {
                    var foundFile = Directory.GetFiles(mediaDir, fileName, SearchOption.AllDirectories)
                        .FirstOrDefault(f => allowedExtensions.Contains(Path.GetExtension(f)));
                    if (foundFile is null)
                    {
                        stats.Errors.Add($"Person Id={old.Id}: файл не найден '{fileName}'");
                        continue;
                    }
                    filePath = foundFile;
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length == 0)
                    {
                        stats.Errors.Add($"Person Id={old.Id}: файл пустой '{fileName}'");
                        continue;
                    }

                    var hash = ComputeSha256(filePath);
                    var data = File.ReadAllBytes(filePath);
                    var mimeType = GetMimeType(Path.GetExtension(filePath));

                    mediaTasks.Add((old, newPerson, fileName, filePath, hash, data, mimeType));
                }
                catch (Exception ex)
                {
                    stats.Errors.Add($"Person Id={old.Id}: файл '{fileName}': {ex.Message}");
                    _logger.LogError(ex, "Ошибка чтения медиа для Person Id={Id}, файл {File}", old.Id, fileName);
                }
            }
        }

        stats.MediaFilesFound = found;

        // Этап 1: создаём MediaAttachment для уникальных хэшей, которых нет в БД и в этом запуске
        int createdMedia = 0;
        foreach (var task in mediaTasks)
        {
            if (existingMediaByHash.ContainsKey(task.Hash) || newMediaByHash.ContainsKey(task.Hash))
                continue;

            var media = new MediaAttachment
            {
                OriginalFileName = Path.GetFileName(task.FilePath),
                MimeType = task.MimeType,
                SizeBytes = task.Data.Length,
                ContentHash = task.Hash,
                Data = task.Data,
                CreatedAt = DateTime.UtcNow
            };
            targetContext.MediaAttachments.Add(media);
            newMediaByHash[task.Hash] = media;
            createdMedia++;
        }

        _logger.LogInformation("Создано новых записей MediaAttachment: {Count}", createdMedia);

        // Сохраняем, чтобы EF присвоил Id новым медиа
        if (createdMedia > 0)
        {
            targetContext.SaveChangesAsync().GetAwaiter().GetResult();
        }

        // Теперь у всех новых медиа есть Id — дополняем словарь
        foreach (var kvp in newMediaByHash)
        {
            existingMediaByHash[kvp.Key] = kvp.Value.Id;
        }

        // Этап 2: создаём MediaAttachmentLink для каждой персоны
        int processed = 0;
        int errors = 0;
        var addedLinks = new HashSet<(int MediaId, int OwnerId)>();

        foreach (var task in mediaTasks)
        {
            int mediaId = existingMediaByHash[task.Hash];

            if (addedLinks.Contains((mediaId, task.New.Id)))
                continue;

            targetContext.MediaAttachmentLinks.Add(new MediaAttachmentLink
            {
                MediaAttachmentId = mediaId,
                OwnerType = MediaOwnerType.Person,
                OwnerId = task.New.Id
            });

            addedLinks.Add((mediaId, task.New.Id));
            processed++;
        }

        stats.MediaFilesProcessed = processed;
        stats.MediaErrors = errors;
        _logger.LogInformation("Найдено ссылок на медиа: {Count}", found);
        _logger.LogInformation("Обработано медиа-ссылок: {Count}", processed);
        _logger.LogInformation("Ошибок медиа: {Count}", errors);
    }

    private static List<string> ParseMediaInformation(string? mediaInformation)
    {
        if (string.IsNullOrWhiteSpace(mediaInformation))
            return new List<string>();

        return mediaInformation
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".avi" => "video/x-msvideo",
        ".wmv" => "video/x-ms-wmv",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".wma" => "audio/x-ms-wma",
        ".m4a" => "audio/mp4",
        ".mov" => "video/quicktime",
        _ => "application/octet-stream"
    };
}
