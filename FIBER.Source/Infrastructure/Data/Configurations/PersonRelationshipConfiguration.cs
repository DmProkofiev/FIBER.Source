using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class PersonRelationshipConfiguration : IEntityTypeConfiguration<PersonRelationship>
{
    public void Configure(EntityTypeBuilder<PersonRelationship> entity)
    {
        entity.ToTable("PersonRelationships");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Kind).HasConversion<int>();
        entity.Property(e => e.Notes).HasColumnType("TEXT");

        entity.HasIndex(e => new { e.PersonId, e.RelatedPersonId, e.Kind })
            .IsUnique()
            .HasDatabaseName("UQ_PersonRelationship_Natural");

        entity.HasIndex(e => e.RelatedPersonId)
            .HasDatabaseName("IX_PersonRelationship_RelatedPersonId");

        entity.HasOne(e => e.Person)
            .WithMany(p => p.Relationships)
            .HasForeignKey(e => e.PersonId)
            .HasConstraintName("FK_PersonRelationship_Person")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.RelatedPerson)
            .WithMany(p => p.ReverseRelationships)
            .HasForeignKey(e => e.RelatedPersonId)
            .HasConstraintName("FK_PersonRelationship_RelatedPerson")
            .OnDelete(DeleteBehavior.Cascade);
    }
}