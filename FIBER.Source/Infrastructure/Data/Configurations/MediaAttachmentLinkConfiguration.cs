using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Infrastructure.Data.Configurations
{
    public class MediaAttachmentLinkConfiguration : IEntityTypeConfiguration<MediaAttachmentLink>
    {
        public void Configure(EntityTypeBuilder<MediaAttachmentLink> entity)
        {
            entity.ToTable("MediaAttachmentLinks");
            entity.HasKey(e => new { e.MediaAttachmentId, e.OwnerType, e.OwnerId });

            entity.Property(e => e.OwnerType).HasConversion<int>();

            entity.HasIndex(e => new { e.OwnerType, e.OwnerId })
                .HasDatabaseName("IX_MediaAttachmentLink_Owner");

            entity.HasOne(e => e.MediaAttachment)
                .WithMany(m => m.Links)
                .HasForeignKey(e => e.MediaAttachmentId)
                .HasConstraintName("FK_MediaAttachmentLink_Media")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
