using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> entity)
    {
        entity.ToTable("Companies");
        entity.HasKey("Id");

        entity.Property(e => e.Name).HasMaxLength(300).IsRequired().UseCollation("NOCASE");
        entity.Property(e => e.RegistrationNumber).HasMaxLength(50);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.Phone).HasMaxLength(50);
        entity.Property(e => e.Mail).HasMaxLength(200);
        entity.Property(e => e.Website).HasMaxLength(200);
        entity.Property(e => e.Notes).HasColumnType("TEXT");

        entity.HasIndex("Name").HasDatabaseName("IX_Company_Name");
    }
}