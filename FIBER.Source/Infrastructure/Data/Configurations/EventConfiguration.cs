using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> entity)
    {
        entity.ToTable("Events");
        entity.HasKey("Id");

        entity.Property(e => e.Title).HasMaxLength(300).IsRequired().UseCollation("NOCASE");
        entity.Property(e => e.Description).HasColumnType("TEXT");
        entity.Property(e => e.Notes).HasColumnType("TEXT");

        entity.HasIndex("Date").HasDatabaseName("IX_Event_Date");
        entity.HasIndex("Title").HasDatabaseName("IX_Event_Title");
        entity.HasIndex("OrganizerCompanyId").HasDatabaseName("IX_Event_OrganizerCompanyId");
        entity.HasIndex("OrganizerPersonId").HasDatabaseName("IX_Event_OrganizerPersonId");

        entity.HasOne(e => e.OrganizerCompany)
            .WithMany(c => c.OrganizedEvents)
            .HasForeignKey(e => e.OrganizerCompanyId)
            .HasConstraintName("FK_Event_OrganizerCompany")
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.OrganizerPerson)
            .WithMany(p => p.OrganizerEvents)
            .HasForeignKey(e => e.OrganizerPersonId)
            .HasConstraintName("FK_Event_OrganizerPerson")
            .OnDelete(DeleteBehavior.SetNull);
    }
}