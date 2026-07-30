using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nagomi.Api.Features.ProviderIntegration;

namespace Nagomi.Api.Infrastructure.Persistence;

internal sealed class TransportProviderConfiguration : IEntityTypeConfiguration<TransportProvider>
{
    public void Configure(EntityTypeBuilder<TransportProvider> entity)
    {
        entity.ToTable("transport_providers");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.Code).IsUnique();
        entity.HasIndex(x => x.QueueName).IsUnique();
        entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.QueueName).HasMaxLength(200).IsRequired();
    }
}

internal sealed class TransportContractConfiguration : IEntityTypeConfiguration<TransportContract>
{
    public void Configure(EntityTypeBuilder<TransportContract> entity)
    {
        entity.ToTable("transport_contracts");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.Code).IsUnique();
        entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(250).IsRequired();
    }
}

internal sealed class ProviderContractRouteConfiguration : IEntityTypeConfiguration<ProviderContractRoute>
{
    public void Configure(EntityTypeBuilder<ProviderContractRoute> entity)
    {
        entity.ToTable("provider_contract_routes");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.ProviderId, x.ContractId }).IsUnique();
        entity.HasIndex(x => x.ContractId).IsUnique().HasFilter("\"IsActive\" = TRUE");
        entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProviderNotificationConfiguration : IEntityTypeConfiguration<ProviderNotification>
{
    public void Configure(EntityTypeBuilder<ProviderNotification> entity)
    {
        entity.ToTable("provider_notifications");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.MessageId).IsUnique();
        entity.HasIndex(x => new { x.State, x.NextAttemptAt });
        entity.HasIndex(x => new { x.ProviderId, x.EntityPublicId, x.State });
        entity.Property(x => x.ContractCode).HasMaxLength(50).IsRequired();
        entity.Property(x => x.MessageType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.EntityPublicId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.RetrievalUrl).HasMaxLength(500).IsRequired();
        entity.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.LastFailureCode).HasMaxLength(100);
    }
}

internal sealed class ProviderCommandReceiptConfiguration : IEntityTypeConfiguration<ProviderCommandReceipt>
{
    public void Configure(EntityTypeBuilder<ProviderCommandReceipt> entity)
    {
        entity.ToTable("provider_command_receipts");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.ProviderId, x.IdempotencyKey }).IsUnique();
        entity.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        entity.Property(x => x.CommandType).HasMaxLength(100).IsRequired();
        entity.Property(x => x.EntityPublicId).HasMaxLength(100).IsRequired();
        entity.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ResponseBody).HasColumnType("text");
    }
}
