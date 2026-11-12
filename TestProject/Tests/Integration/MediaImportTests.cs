using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services;
using FIBER.Source.Services.MediaManagement;
using FIBER.Source.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestProject.Tests.Integration;

public class MediaImportTests
{
    [Theory]
    [InlineData(MediaOwnerType.Person, false)]
    [InlineData(MediaOwnerType.Person, true)]
    [InlineData(MediaOwnerType.Company, false)]
    [InlineData(MediaOwnerType.Company, true)]
    [InlineData(MediaOwnerType.Event, false)]
    [InlineData(MediaOwnerType.Event, true)]
    public async Task DroppedFileIsPersistedAndDeduplicated(MediaOwnerType ownerType, bool hasExistingMedia)
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new ContextFactory(new DbContextOptionsBuilder<FiberSourceDbContext>()
            .UseSqlite(connection).Options);
        await using (var context = factory.CreateDbContext())
            await context.Database.EnsureCreatedAsync();

        var service = new MediaService(factory,
            new AuditLogService(factory, NullLogger<AuditLogService>.Instance),
            null!, NullLogger<MediaService>.Instance);
        // These scenarios do not use playback or the media selection dialog.
        var management = new MediaManagementService(service, null!);
        var pending = new ObservableCollection<MediaItemViewModel>();
        var source = Path.Combine(Path.GetTempPath(), $"fiber-import-test-{Guid.NewGuid():N}.wav");
        var bytes = RandomNumberGenerator.GetBytes(128);
        try
        {
            if (hasExistingMedia)
            {
                await File.WriteAllBytesAsync(source, RandomNumberGenerator.GetBytes(64));
                var initial = await service.SaveToTempAsync([source]);
                await service.CommitTempFilesAsync(initial, ownerType, 1);
            }

            await File.WriteAllBytesAsync(source, bytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            Assert.Null(await service.GetMediaIdByContentHashAsync(hash));

            Assert.Equal(0, await management.HandleFilesDropAsync(pending, [source]));
            Assert.Null(Assert.Single(pending).MediaId);
            Assert.Equal(1, await management.HandleFilesDropAsync(pending, [source]));
            Assert.Single(pending);

            // The same TempFiles routing is used by all six Create/Update VMs.
            await SavePendingAsync();
            var id = await service.GetMediaIdByContentHashAsync(hash);
            Assert.True(id is > 0);
            Assert.Equal(bytes, await service.GetMediaDataAsync(id.Value));

            await management.HandleFilesDropAsync(pending, [source]);
            Assert.Equal(id, Assert.Single(pending).MediaId);
            await SavePendingAsync();
            await management.LinkExistingMediaAsync([id.Value], ownerType, 2);

            await using var verify = factory.CreateDbContext();
            Assert.Equal(hasExistingMedia ? 2 : 1, await verify.MediaAttachments.CountAsync());
            Assert.Equal(2, await verify.MediaAttachmentLinks.CountAsync(l => l.MediaAttachmentId == id));
            var stored = await verify.MediaAttachments.SingleAsync(m => m.Id == id);
            Assert.Equal("audio/wav", stored.MimeType);
            Assert.Equal(bytes.Length, stored.SizeBytes);
            Assert.Equal(hasExistingMedia ? 2 : 1, (await service.GetMediaForOwnerAsync(ownerType, 1)).Count);
        }
        finally
        {
            service.DeleteTempFiles(pending.Select(m => m.ToDto()));
            File.Delete(source);
        }

        async Task SavePendingAsync()
        {
            await management.LinkExistingMediaAsync(pending.Where(m => m.MediaId.HasValue)
                .Select(m => m.MediaId!.Value), ownerType, 1);
            await service.CommitTempFilesAsync(pending.Where(m => !m.MediaId.HasValue)
                .Select(m => m.ToDto()).ToList(), ownerType, 1);
            service.DeleteTempFiles(pending.Select(m => m.ToDto()));
            pending.Clear();
        }
    }

    private sealed class ContextFactory(DbContextOptions<FiberSourceDbContext> options)
        : IDbContextFactory<FiberSourceDbContext>
    {
        public FiberSourceDbContext CreateDbContext() => new(options);
    }
}
