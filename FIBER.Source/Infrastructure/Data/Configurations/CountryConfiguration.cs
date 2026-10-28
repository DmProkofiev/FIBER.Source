using System.Linq;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> entity)
    {
        entity.ToTable("Countries");
        entity.HasKey("Code");

        entity.Property(e => e.Code).HasMaxLength(2).IsRequired();
        entity.Property(e => e.NameRu).HasMaxLength(200).IsRequired();
        entity.Property(e => e.NameEn).HasMaxLength(200).IsRequired();

        entity.HasData(CountrySeedData.All.Cast<object>().ToArray());
    }
}