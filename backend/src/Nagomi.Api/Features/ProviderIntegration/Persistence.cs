using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ProviderIntegration;

public interface IProviderIntegrationDb
{
    DbSet<TransportProvider> TransportProviders { get; }
    DbSet<TransportContract> TransportContracts { get; }
    DbSet<ProviderContractRoute> ProviderContractRoutes { get; }
    DbSet<ProviderNotification> ProviderNotifications { get; }
    DbSet<ProviderCommandReceipt> ProviderCommandReceipts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public static class ProviderIntegrationModelConfiguration
{
    public static ModelBuilder ConfigureProviderIntegration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransportProvider>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.QueueName).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.QueueName).HasMaxLength(200);
        });

        modelBuilder.Entity<TransportContract>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(250);
        });

        modelBuilder.Entity<ProviderContractRoute>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProviderId, x.ContractId }).IsUnique();
            entity.HasIndex(x => x.ContractId).IsUnique().HasFilter("\"IsActive\" = TRUE");
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProviderNotification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.MessageId).IsUnique();
            entity.HasIndex(x => new { x.State, x.NextAttemptAt });
            entity.HasIndex(x => new { x.ProviderId, x.EntityPublicId, x.State });
            entity.Property(x => x.ContractCode).HasMaxLength(50);
            entity.Property(x => x.MessageType).HasMaxLength(100);
            entity.Property(x => x.EntityPublicId).HasMaxLength(100);
            entity.Property(x => x.RetrievalUrl).HasMaxLength(500);
            entity.Property(x => x.LastFailureCode).HasMaxLength(100);
        });

        modelBuilder.Entity<ProviderCommandReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProviderId, x.IdempotencyKey }).IsUnique();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            entity.Property(x => x.CommandType).HasMaxLength(100);
            entity.Property(x => x.EntityPublicId).HasMaxLength(100);
            entity.Property(x => x.RequestHash).HasMaxLength(64);
        });

        return modelBuilder;
    }
}
