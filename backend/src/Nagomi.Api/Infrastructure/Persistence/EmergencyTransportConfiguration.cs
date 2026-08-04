using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nagomi.Api.Features.EmergencyTransports;

namespace Nagomi.Api.Infrastructure.Persistence;

internal sealed class EmergencyTransportRecordConfiguration : IEntityTypeConfiguration<EmergencyTransportRecord>
{
    public void Configure(EntityTypeBuilder<EmergencyTransportRecord> entity)
    {
        entity.ToTable("emergency_transports");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.Property(x => x.PublicId).HasMaxLength(40).IsRequired();
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ContactPhone).HasMaxLength(50);
        entity.Property(x => x.Observations).HasMaxLength(2000);

        entity.OwnsOne(x => x.Incident, incident =>
        {
            incident.Property(x => x.Latitude).HasColumnType("numeric(9,6)").IsRequired();
            incident.Property(x => x.Longitude).HasColumnType("numeric(9,6)").IsRequired();
            incident.Property(x => x.Address).HasMaxLength(500);
            incident.Property(x => x.Municipality).HasMaxLength(200);
            incident.Property(x => x.Notes).HasMaxLength(2000);
        });
    }
}
