using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ReferenceData;

public static class ReferenceDataModelConfiguration
{
    public static ModelBuilder ConfigureReferenceData(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IneAutonomousCommunity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(2);
            entity.Property(x => x.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<IneProvince>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.AutonomousCommunityCode);
            entity.Property(x => x.Code).HasMaxLength(2);
            entity.Property(x => x.AutonomousCommunityCode).HasMaxLength(2);
            entity.Property(x => x.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<IneMunicipality>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.ProvinceCode);
            entity.Property(x => x.Code).HasMaxLength(5);
            entity.Property(x => x.ProvinceCode).HasMaxLength(2);
            entity.Property(x => x.AutonomousCommunityCode).HasMaxLength(2);
            entity.Property(x => x.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<TransportReason>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(250);
        });

        modelBuilder.Entity<HealthcareFacility>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.Ccn).IsUnique().HasFilter("\"Ccn\" IS NOT NULL");
            entity.HasIndex(x => x.Codcnh).IsUnique().HasFilter("\"Codcnh\" IS NOT NULL");
            entity.HasIndex(x => new { x.Name, x.MunicipalityCode });
            entity.Property(x => x.Name).HasMaxLength(250);
            entity.Property(x => x.Ccn).HasMaxLength(50);
            entity.Property(x => x.Codcnh).HasMaxLength(50);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.MunicipalityCode).HasMaxLength(5);
            entity.Property(x => x.ProvinceCode).HasMaxLength(2);
            entity.Property(x => x.AutonomousCommunityCode).HasMaxLength(2);
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
        });

        return modelBuilder;
    }
}
