using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class PersonAliasConfiguration : IEntityTypeConfiguration<PersonAlias>
{
    public void Configure(EntityTypeBuilder<PersonAlias> entity)
    {
        entity.ToTable("PersonAliases");
        entity.HasKey("Id");

        entity.Property(e => e.Value).HasMaxLength(200).IsRequired().UseCollation("NOCASE");
        entity.Property(e => e.Type).HasConversion<int>();

        entity.HasIndex("Value").HasDatabaseName("IX_PersonAlias_Value");

        entity.HasIndex("PersonId", "IsPrimary")
            .HasDatabaseName("IX_PersonAlias_PersonId_IsPrimary");

        entity.HasIndex("PersonId")
            .HasDatabaseName("UQ_PersonAlias_Primary")
            .IsUnique()
            .HasFilter("\"IsPrimary\" = 1");

        entity.HasOne(e => e.Person)
            .WithMany(p => p.Aliases)
            .HasForeignKey("PersonId")
            .HasConstraintName("FK_PersonAlias_Person")
            .OnDelete(DeleteBehavior.Cascade);
    }
}