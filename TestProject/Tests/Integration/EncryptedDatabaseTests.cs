using System.IO;
using System.Security.Cryptography;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services;
using FIBER.Source.Services.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestProject.Tests.Integration;

public sealed class EncryptedDatabaseTests
{
    [Fact]
    public async Task RekeyAndExportsPreserveRowsAndBlobAndUseCurrentKey()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var data = RandomNumberGenerator.GetBytes(1024 * 1024);
        var source = Path.Combine(fixture.Directory, "sample.wav");
        await File.WriteAllBytesAsync(source, data);
        var pending = await fixture.Media.SaveToTempAsync([source, source]);
        await fixture.Media.CommitTempFilesAsync(pending, MediaOwnerType.Person, 1);
        var hash = Convert.ToHexString(SHA256.HashData(data));
        var id = (await fixture.Media.GetMediaIdByContentHashAsync(hash))!.Value;
        Assert.Empty(Assert.Single(await fixture.Media.GetAllMediaAsync()).Media.FilePath);
        Assert.Empty(await fixture.Media.GetAllImagesAsync());

        Assert.False(await fixture.Database.TryUnlockAsync("wrong password"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Database.ChangePasswordAsync("wrong password", "rejected"));
        Assert.Equal("initial", fixture.Database.CurrentPassword);
        await fixture.Database.ChangePasswordAsync("initial", "новый'пароль");
        Assert.False(await fixture.Database.TryUnlockAsync("initial"));
        Assert.True(await fixture.Database.TryUnlockAsync("новый'пароль"));

        await using (var context = await fixture.Factory.CreateDbContextAsync())
        {
            context.Companies.Add(new Company { Name = "После смены ключа" });
            await context.SaveChangesAsync();
            Assert.Single(await context.MediaAttachmentLinks.ToListAsync());
        }
        Assert.Equal(data, await fixture.Media.GetMediaDataAsync(id));
        var settings = new SettingsDataService(fixture.Factory,
            new CompanyService(fixture.Factory, fixture.Media, fixture.Audit),
            new EventService(fixture.Factory, fixture.Media, fixture.Audit), fixture.Media, fixture.Audit);
        var snapshot = await settings.LoadAsync();
        Assert.Single(snapshot.Companies);
        Assert.Single(snapshot.Media);
        var playback = await fixture.Media.GetMediaFilePathAsync(id);
        Assert.Equal(data, await File.ReadAllBytesAsync(playback));
        await File.WriteAllTextAsync(Path.Combine(fixture.Directory, "encrypted.db"), "old export");

        foreach (var encrypted in new[] { true, false })
        {
            var path = Path.Combine(fixture.Directory, encrypted ? "encrypted.db" : "plaintext.db");
            await fixture.Database.ExportAsync(path, encrypted);
            await fixture.Database.ExportAsync(path, encrypted); // replacement must also work
            await using var copy = new FiberSourceDbContext(new DbContextOptionsBuilder<FiberSourceDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = path, Password = encrypted ? "новый'пароль" : "", Pooling = false
                }.ToString()).Options);
            Assert.Equal("После смены ключа", (await copy.Companies.SingleAsync()).Name);
            Assert.Equal(data, (await copy.MediaAttachments.SingleAsync()).Data);
            Assert.Empty(await copy.Database.GetPendingMigrationsAsync());
        }
        await fixture.Media.DeleteTempFilesAsync([new(id, playback, "sample.wav", "audio/wav", data.Length, hash, false, false, true)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Database.ExportAsync(fixture.Database.DatabasePath, true));
    }

    [Fact]
    public async Task RekeyWaitsForLiveContextAndNewContextsUseNewPassword()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var context = await fixture.Factory.CreateDbContextAsync();
        var change = fixture.Database.ChangePasswordAsync("initial", "changed");
        Assert.False(change.IsCompleted);
        Assert.Equal(0, await context.Companies.CountAsync());
        await context.DisposeAsync();
        await change.WaitAsync(TimeSpan.FromSeconds(15));
        await using var next = await fixture.Factory.CreateDbContextAsync();
        next.Companies.Add(new Company { Name = "New key" });
        await next.SaveChangesAsync();
    }

    [Fact]
    public async Task FailedPersonCreateRollsBackAndEventLinksAreSavedUpdatedAndRemoved()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var service = new PersonService(fixture.Factory, new SearchStrategyFactory([new SearchByIDStrategy()]), fixture.Audit, fixture.Media);
        var input = new PersonInput("Иванов", "Иван", null, Gender.Male, null,
            "", "", "", "", null, "", "", "", "", "");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateIdentifiedAsync(input with
        {
            EventParticipations = [new(null, null, null, int.MaxValue, EventParticipantRole.Participant)]
        }));
        int eventId;
        await using (var context = await fixture.Factory.CreateDbContextAsync())
        {
            Assert.Empty(await context.People.ToListAsync());
            var ev = new Event { Title = "Проверка" };
            context.Events.Add(ev);
            await context.SaveChangesAsync();
            eventId = ev.Id;
        }
        var person = await service.CreateIdentifiedAsync(input with
        {
            EventParticipations = [new(null, null, null, eventId, EventParticipantRole.Participant)]
        });
        int linkId;
        await using (var context = await fixture.Factory.CreateDbContextAsync())
            linkId = (await context.EventParticipants.SingleAsync()).Id;

        await service.UpdateAsync(person.Id, input with
        {
            EventParticipations = [new(linkId, person.Id, null, eventId, EventParticipantRole.Witness)]
        });
        await using (var context = await fixture.Factory.CreateDbContextAsync())
            Assert.Equal(EventParticipantRole.Witness, (await context.EventParticipants.SingleAsync()).Role);

        await service.UpdateAsync(person.Id, input with { EventParticipations = [] });
        await using (var context = await fixture.Factory.CreateDbContextAsync())
            Assert.Empty(await context.EventParticipants.ToListAsync());
    }

    [Fact]
    public async Task SaveFailureReportsActualSqliteConstraint()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var context = await fixture.Factory.CreateDbContextAsync();
        context.People.Add(new Person { Status = IdentityStatus.Identified });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_Person_Status_FullName", error.Message);
        Assert.Contains("19/275", error.Message);
    }

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "opencode", "fiber-tests-" + Guid.NewGuid().ToString("N"));
        public DatabaseService Database { get; }
        public RuntimeDbContextFactory Factory { get; }
        public AuditLogService Audit { get; }
        public MediaService Media { get; }

        private DatabaseFixture()
        {
            var access = new DatabaseAccessCoordinator();
            Database = new DatabaseService(access, Path.Combine(Directory, "fiber.db"));
            Factory = new RuntimeDbContextFactory(Database, access, NullLoggerFactory.Instance);
            Audit = new AuditLogService(Factory, NullLogger<AuditLogService>.Instance);
            Media = new MediaService(Factory, Audit, NullLogger<MediaService>.Instance);
        }

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.Batteries_V2.Init();
            var fixture = new DatabaseFixture();
            System.IO.Directory.CreateDirectory(fixture.Directory);
            await fixture.Database.CreateDatabaseAsync("initial");
            await using var context = await fixture.Factory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            return fixture;
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            System.IO.Directory.Delete(Directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
