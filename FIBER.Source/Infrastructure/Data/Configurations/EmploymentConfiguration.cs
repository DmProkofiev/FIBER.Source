using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class EmploymentConfiguration : IEntityTypeConfiguration<Employment>
{
    public void Configure(EntityTypeBuilder<Employment> entity)
    {
        entity.ToTable("Employments");
        entity.HasKey("PersonId", "CompanyId");

        entity.Property(e => e.Position).HasMaxLength(200);

        entity.HasOne(e => e.Person)
            .WithMany(p => p.Employments)
            .HasForeignKey("PersonId")
            .HasConstraintName("FK_Employment_Person")
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Company)
            .WithMany(c => c.Employees)
            .HasForeignKey("CompanyId")
            .HasConstraintName("FK_Employment_Company")
            .OnDelete(DeleteBehavior.Cascade);
    }
}