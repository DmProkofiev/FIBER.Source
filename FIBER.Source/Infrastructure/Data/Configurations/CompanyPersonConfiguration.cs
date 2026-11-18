using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class CompanyPersonConfiguration : IEntityTypeConfiguration<CompanyPerson>
{
    public void Configure(EntityTypeBuilder<CompanyPerson> entity)
    {
        entity.ToTable("CompanyPeople");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.PersonRole).HasMaxLength(100).IsRequired();
        entity.Property(e => e.CompanyRole).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Notes).HasColumnType("TEXT");

        entity.HasIndex(e => new { e.CompanyId, e.PersonId })
            .IsUnique()
            .HasDatabaseName("UQ_CompanyPerson_Natural");

        entity.HasIndex(e => e.PersonId)
            .HasDatabaseName("IX_CompanyPerson_PersonId");

        entity.HasOne(e => e.Company)
            .WithMany(c => c.CompanyPeople)
            .HasForeignKey(e => e.CompanyId)
            .HasConstraintName("FK_CompanyPerson_Company")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Person)
            .WithMany(p => p.CompanyPeople)
            .HasForeignKey(e => e.PersonId)
            .HasConstraintName("FK_CompanyPerson_Person")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
