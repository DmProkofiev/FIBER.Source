using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> entity)
    {
        //entity.ToTable("AuditLog");
        entity.ToTable("AuditLogEntries");
        entity.HasKey("Id");

        entity.Property(e => e.Level).HasMaxLength(20).IsRequired();
        entity.Property(e => e.Action).HasMaxLength(200).IsRequired();
        entity.Property(e => e.ActionObject).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Details).HasColumnType("TEXT");

        entity.HasIndex("Timestamp").HasDatabaseName("IX_AuditLog_Timestamp");
    }
}