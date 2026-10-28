using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIBER.Source.Infrastructure.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> entity)
    {
        entity.ToTable("People", t =>
        {
            t.HasCheckConstraint(
                "CK_Person_Status_FullName",
                "(Status = 0 AND LastName IS NULL AND FirstName IS NULL AND SecondName IS NULL) OR (Status = 1 AND LastName IS NOT NULL AND FirstName IS NOT NULL)");
        });

        entity.HasKey("Id");

        entity.Property(e => e.LastName).HasMaxLength(100).UseCollation("NOCASE");
        entity.Property(e => e.FirstName).HasMaxLength(100).UseCollation("NOCASE");
        entity.Property(e => e.SecondName).HasMaxLength(100).UseCollation("NOCASE");
        entity.Property(e => e.Gender).HasConversion<int>();

        entity.Property(e => e.BirthDate).HasColumnType("TEXT");

        entity.Property(e => e.Passport).HasMaxLength(2000);
        entity.Property(e => e.Job).HasMaxLength(200);
        entity.Property(e => e.Phone).HasMaxLength(50);
        entity.Property(e => e.City).HasMaxLength(100);
        entity.Property(e => e.CountryCode).HasMaxLength(2);
        entity.Property(e => e.Car).HasMaxLength(100);
        entity.Property(e => e.Mail).HasMaxLength(200);
        entity.Property(e => e.SocialMedia).HasColumnType("TEXT");
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.Notes).HasColumnType("TEXT");

        entity.HasIndex("LastName", "FirstName")
            .HasDatabaseName("IX_Person_Name_Identified")
            .HasFilter("\"Status\" = 1");

        entity.HasIndex("Status", "CreatedAt")
            .HasDatabaseName("IX_Person_Status_CreatedAt");

        entity.HasIndex("BirthDate")
            .HasDatabaseName("IX_Person_BirthDate")
            .HasFilter("\"BirthDate\" IS NOT NULL");

        entity.HasOne(e => e.Country)
            .WithMany()
            .HasForeignKey("CountryCode")
            .HasConstraintName("FK_Person_Country")
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.MergedInto)
            .WithMany()
            .HasForeignKey("MergedIntoPersonId")
            .HasConstraintName("FK_Person_MergedInto")
            .OnDelete(DeleteBehavior.Restrict);
    }
}