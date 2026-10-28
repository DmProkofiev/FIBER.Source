using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности MediaAttachment с BLOB-полем Data.
/// </summary>
public class MediaAttachmentConfiguration : IEntityTypeConfiguration<MediaAttachment>
{
    /// <summary>Настраивает таблицу, поля, индексы и ограничения.</summary>
    public void Configure(EntityTypeBuilder<MediaAttachment> entity)
    {
        entity.ToTable("MediaAttachments");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.OriginalFileName).HasMaxLength(500).IsRequired();
        entity.Property(e => e.MimeType).HasMaxLength(100).IsRequired();
        entity.Property(e => e.ContentHash).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Data).IsRequired();

        entity.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_MediaAttachment_CreatedAt");

        entity.HasIndex(e => e.ContentHash)
            .HasDatabaseName("UQ_MediaAttachment_ContentHash")
            .IsUnique()
            .HasFilter("\"ContentHash\" <> ''");
    }
}