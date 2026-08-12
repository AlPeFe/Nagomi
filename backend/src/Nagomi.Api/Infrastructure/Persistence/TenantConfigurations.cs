using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nagomi.Api.Features.Tenant;

namespace Nagomi.Api.Infrastructure.Persistence;

internal sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> entity)
    {
        entity.ToTable("tenant_settings");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Capabilities).HasConversion<string>().HasMaxLength(64);
    }
}

internal sealed class TransportClientConfiguration : IEntityTypeConfiguration<TransportClient>
{
    public void Configure(EntityTypeBuilder<TransportClient> entity)
    {
        entity.ToTable("transport_clients");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.Property(x => x.PublicId).HasMaxLength(40);
        entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
        entity.Property(x => x.TaxId).HasMaxLength(50);
        entity.Property(x => x.ContactPerson).HasMaxLength(200);
        entity.Property(x => x.Phone).HasMaxLength(50);
        entity.Property(x => x.Email).HasMaxLength(200);
        entity.Property(x => x.Address).HasMaxLength(500);
    }
}
