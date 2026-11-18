using FIBER.Source.Models;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace FIBER.Source.Infrastructure.Data;

public class FiberSourceDbContext : DbContext
{
    private IDisposable? _accessLease;

    public FiberSourceDbContext(DbContextOptions<FiberSourceDbContext> options, IDisposable? accessLease = null)
        : base(options)
    {
        _accessLease = accessLease;
    }

    public override void Dispose()
    {
        try { base.Dispose(); }
        finally { Interlocked.Exchange(ref _accessLease, null)?.Dispose(); }
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync().ConfigureAwait(false); }
        finally { Interlocked.Exchange(ref _accessLease, null)?.Dispose(); }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
        {
            var reason = sqlite.SqliteExtendedErrorCode switch
            {
                787 => "Связанная запись отсутствует или была удалена.",
                1555 or 2067 => "Такая запись или связь уже существует.",
                275 => "Данные нарушают ограничения целостности.",
                1299 => "Не заполнено обязательное поле.",
                _ => "Ошибка записи в базу данных."
            };
            throw new DbUpdateException($"{reason} SQLite {sqlite.SqliteErrorCode}/{sqlite.SqliteExtendedErrorCode}: {sqlite.Message}", ex, ex.Entries);
        }
    }

    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonAlias> PersonAliases => Set<PersonAlias>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<MediaAttachment> MediaAttachments => Set<MediaAttachment>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<PersonRelationship> PersonRelationships => Set<PersonRelationship>();
    public DbSet<CompanyInteraction> CompanyInteractions => Set<CompanyInteraction>();
    public DbSet<CompanyPerson> CompanyPeople => Set<CompanyPerson>();
    public DbSet<EventEvent> EventEvents => Set<EventEvent>();
    public DbSet<MediaAttachmentLink> MediaAttachmentLinks => Set<MediaAttachmentLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FiberSourceDbContext).Assembly);
    }
}
