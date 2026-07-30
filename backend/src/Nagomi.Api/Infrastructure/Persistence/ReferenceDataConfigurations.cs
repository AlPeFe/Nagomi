using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nagomi.Api.Features.ReferenceData;

namespace Nagomi.Api.Infrastructure.Persistence;

internal sealed class IneAutonomousCommunityConfiguration : IEntityTypeConfiguration<IneAutonomousCommunity>
{
    public void Configure(EntityTypeBuilder<IneAutonomousCommunity> entity)
    {
        entity.ToTable("ine_autonomous_communities");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.Code).IsUnique();
        entity.Property(x => x.Code).HasMaxLength(2).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
    }
}

internal sealed class IneProvinceConfiguration : IEntityTypeConfiguration<IneProvince>
{
    public void Configure(EntityTypeBuilder<IneProvince> entity)
    {
        entity.ToTable("ine_provinces");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.Code).IsUnique();
        entity.HasIndex(x => x.AutonomousCommunityCode);
        entity.Property(x => x.Code).HasMaxLength(2).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
        entity.Property(x => x.AutonomousCommunityCode).HasMaxLength(2).IsRequired();
    }
}

internal sealed class IneMunicipalityConfiguration : IEntityTypeConfiguration<IneMunicipality>
{
    public void Configure(EntityTypeBuilder<IneMunicipality> entity)
    {
        entity.ToTable("ine_municipalities");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.Code).IsUnique();
        entity.HasIndex(x => x.ProvinceCode);
        entity.Property(x => x.Code).HasMaxLength(5).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
        entity.Property(x => x.ProvinceCode).HasMaxLength(2).IsRequired();
        entity.Property(x => x.AutonomousCommunityCode).HasMaxLength(2).IsRequired();
    }
}

internal sealed class TransportReasonConfiguration : IEntityTypeConfiguration<TransportReason>
{
    public void Configure(EntityTypeBuilder<TransportReason> entity)
    {
        entity.ToTable("transport_reasons");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.Code).IsUnique();
        entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(250).IsRequired();
    }
}

internal sealed class HealthcareFacilityConfiguration : IEntityTypeConfiguration<HealthcareFacility>
{
    public void Configure(EntityTypeBuilder<HealthcareFacility> entity)
    {
        entity.ToTable("healthcare_facilities");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.HasIndex(x => x.Ccn).IsUnique().HasFilter("\"Ccn\" IS NOT NULL");
        entity.HasIndex(x => x.Codcnh).IsUnique().HasFilter("\"Codcnh\" IS NOT NULL");
        entity.HasIndex(x => new { x.Name, x.MunicipalityCode });
        entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
        entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Ccn).HasMaxLength(50);
        entity.Property(x => x.Codcnh).HasMaxLength(50);
        entity.Property(x => x.OfficialAddressText).HasMaxLength(500);
        entity.Property(x => x.Street).HasMaxLength(250);
        entity.Property(x => x.Number).HasMaxLength(30);
        entity.Property(x => x.Block).HasMaxLength(30);
        entity.Property(x => x.Staircase).HasMaxLength(30);
        entity.Property(x => x.Floor).HasMaxLength(30);
        entity.Property(x => x.Door).HasMaxLength(30);
        entity.Property(x => x.AdditionalDetails).HasMaxLength(500);
        entity.Property(x => x.PostalCode).HasMaxLength(20);
        entity.Property(x => x.MunicipalityCode).HasMaxLength(5);
        entity.Property(x => x.ProvinceCode).HasMaxLength(2);
        entity.Property(x => x.AutonomousCommunityCode).HasMaxLength(2);
        entity.Property(x => x.Phone).HasMaxLength(50);
        entity.Property(x => x.Notes).HasMaxLength(2000);
        entity.Property(x => x.Latitude).HasPrecision(9, 6);
        entity.Property(x => x.Longitude).HasPrecision(9, 6);
    }
}
