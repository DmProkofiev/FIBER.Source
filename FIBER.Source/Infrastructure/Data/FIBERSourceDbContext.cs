using FIBER.Source.Models;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Infrastructure.Data;

public class FiberSourceDbContext : DbContext
{
    public FiberSourceDbContext(DbContextOptions<FiberSourceDbContext> options)
        : base(options)
    {
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
    public DbSet<MediaAttachmentLink> MediaAttachmentLinks => Set<MediaAttachmentLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FiberSourceDbContext).Assembly);
    }
}