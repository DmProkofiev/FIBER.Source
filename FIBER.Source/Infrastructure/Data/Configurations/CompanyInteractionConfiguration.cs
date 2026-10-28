using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Infrastructure.Data.Configurations
{
    public class CompanyInteractionConfiguration : IEntityTypeConfiguration<CompanyInteraction>
    {
        public void Configure(EntityTypeBuilder<CompanyInteraction> entity)
        {
            entity.ToTable("CompanyInteractions");
            entity.HasKey(e => new { e.SourceCompanyId, e.TargetCompanyId });

            entity.Property(e => e.InteractionType).HasMaxLength(100);
            entity.Property(e => e.Notes).HasColumnType("TEXT");

            entity.HasIndex(e => e.TargetCompanyId).HasDatabaseName("IX_CompanyInteraction_TargetCompanyId");

            entity.HasOne(e => e.SourceCompany)
                .WithMany(c => c.OutgoingInteractions)
                .HasForeignKey(e => e.SourceCompanyId)
                .HasConstraintName("FK_CompanyInteraction_Source")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetCompany)
                .WithMany(c => c.IncomingInteractions)
                .HasForeignKey(e => e.TargetCompanyId)
                .HasConstraintName("FK_CompanyInteraction_Target")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
