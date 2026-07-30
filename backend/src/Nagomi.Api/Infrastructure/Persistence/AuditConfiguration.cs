using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nagomi.Api.Features.Audit;

namespace Nagomi.Api.Infrastructure.Persistence;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> entity)
    {
        entity.ToTable("audit_entries");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.EntityType, x.EntityIdentifier, x.ReceivedAt });
        entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EntityIdentifier).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.ActorIdentifier).HasMaxLength(200).IsRequired();
        entity.Property(x => x.ActorDisplayName).HasMaxLength(250).IsRequired();
        entity.Property(x => x.ProviderIdentifier).HasMaxLength(200);
        entity.Property(x => x.ProviderName).HasMaxLength(250);
        entity.HasMany(x => x.Changes)
            .WithOne()
            .HasForeignKey(x => x.AuditEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditChangeConfiguration : IEntityTypeConfiguration<AuditChange>
{
    public void Configure(EntityTypeBuilder<AuditChange> entity)
    {
        entity.ToTable("audit_changes");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.FieldName).HasMaxLength(200).IsRequired();
        entity.Property(x => x.PreviousValue).HasMaxLength(4000);
        entity.Property(x => x.CurrentValue).HasMaxLength(4000);
        entity.Property(x => x.Protection).HasConversion<string>().HasMaxLength(30);
    }
}
