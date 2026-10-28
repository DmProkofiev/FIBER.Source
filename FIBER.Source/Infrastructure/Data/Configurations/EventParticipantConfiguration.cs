using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> entity)
    {
        entity.ToTable("EventParticipants", t =>
        {
            t.HasCheckConstraint(
                "CK_EventParticipant_OneOwner",
                "(PersonId IS NOT NULL AND CompanyId IS NULL) OR (PersonId IS NULL AND CompanyId IS NOT NULL)");
        });
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Role).HasConversion<int>();
        entity.Property(e => e.Notes).HasColumnType("TEXT");

        entity.HasIndex(e => new { e.EventId, e.PersonId })
            .HasDatabaseName("UQ_EventParticipant_EventPerson")
            .IsUnique()
            .HasFilter("\"PersonId\" IS NOT NULL");

        entity.HasIndex(e => new { e.EventId, e.CompanyId })
            .HasDatabaseName("UQ_EventParticipant_EventCompany")
            .IsUnique()
            .HasFilter("\"CompanyId\" IS NOT NULL");

        entity.HasOne(e => e.Event)
            .WithMany(ev => ev.Participants)
            .HasForeignKey(e => e.EventId)
            .HasConstraintName("FK_EventParticipant_Event")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Person)
            .WithMany(p => p.EventParticipations)
            .HasForeignKey(e => e.PersonId)
            .HasConstraintName("FK_EventParticipant_Person")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.EventParticipations)
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("FK_EventParticipant_Company")
            .OnDelete(DeleteBehavior.Cascade);
    }
}