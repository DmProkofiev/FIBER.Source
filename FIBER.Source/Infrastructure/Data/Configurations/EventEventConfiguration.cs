using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class EventEventConfiguration : IEntityTypeConfiguration<EventEvent>
{
    public void Configure(EntityTypeBuilder<EventEvent> entity)
    {
        entity.ToTable("EventEvents");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.RelationshipType).HasColumnType("TEXT");
        entity.Property(e => e.Notes).HasColumnType("TEXT");

        entity.HasIndex(e => new { e.SourceEventId, e.TargetEventId, e.RelationshipType })
            .IsUnique()
            .HasDatabaseName("UQ_EventEvent_Natural");

        entity.HasIndex(e => e.TargetEventId)
            .HasDatabaseName("IX_EventEvent_TargetEventId");

        entity.HasOne(e => e.SourceEvent)
            .WithMany(e => e.EventEvents)
            .HasForeignKey(e => e.SourceEventId)
            .HasConstraintName("FK_EventEvent_SourceEvent")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.TargetEvent)
            .WithMany(e => e.ReverseEventEvents)
            .HasForeignKey(e => e.TargetEventId)
            .HasConstraintName("FK_EventEvent_TargetEvent")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
