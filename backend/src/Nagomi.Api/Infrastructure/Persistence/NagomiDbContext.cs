using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Features.Audit;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.ReferenceData;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.Api.Infrastructure.Persistence;

public sealed class NagomiDbContext(DbContextOptions<NagomiDbContext> options)
    : DbContext(options), INagomiDb, IAuditHistoryQuery, ITransportDb, IProviderIntegrationDb
{
    public DbSet<IneAutonomousCommunity> IneAutonomousCommunities => Set<IneAutonomousCommunity>();
    public DbSet<IneProvince> IneProvinces => Set<IneProvince>();
    public DbSet<IneMunicipality> IneMunicipalities => Set<IneMunicipality>();
    public DbSet<TransportReason> TransportReasons => Set<TransportReason>();
    public DbSet<HealthcareFacility> HealthcareFacilities => Set<HealthcareFacility>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<TransportRequestRecord> TransportRequestRecords => Set<TransportRequestRecord>();
    public DbSet<JourneyRecord> JourneyRecords => Set<JourneyRecord>();
    public DbSet<JourneyStatusRecord> JourneyStatusRecords => Set<JourneyStatusRecord>();
    public DbSet<TransportAuditRecord> TransportAuditRecords => Set<TransportAuditRecord>();
    public DbSet<TransportProvider> TransportProviders => Set<TransportProvider>();
    public DbSet<TransportContract> TransportContracts => Set<TransportContract>();
    public DbSet<ProviderContractRoute> ProviderContractRoutes => Set<ProviderContractRoute>();
    public DbSet<ProviderNotification> ProviderNotifications => Set<ProviderNotification>();
    public DbSet<ProviderCommandReceipt> ProviderCommandReceipts => Set<ProviderCommandReceipt>();

    IQueryable<TransportRequestRecord> ITransportDb.TransportRequests => TransportRequestRecords;
    IQueryable<JourneyRecord> ITransportDb.Journeys => JourneyRecords;
    IQueryable<TransportAuditRecord> ITransportDb.TransportAudit => TransportAuditRecords;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(NagomiDbContext).Assembly,
            type => type.Namespace == typeof(NagomiDbContext).Namespace);
    }

    public void Add(TransportRequestRecord request) => TransportRequestRecords.Add(request);

    public void Add(JourneyRecord journey) => JourneyRecords.Add(journey);

    public void Add(JourneyStatusRecord status) => JourneyStatusRecords.Add(status);

    public void Add(TransportAuditRecord audit) => TransportAuditRecords.Add(audit);

    public void Remove(TransportRequestRecord request) => TransportRequestRecords.Remove(request);

    public async Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(
        string entityType,
        string entityIdentifier,
        CancellationToken cancellationToken) =>
        await AuditEntries
            .AsNoTracking()
            .Include(entry => entry.Changes)
            .Where(entry => entry.EntityType == entityType && entry.EntityIdentifier == entityIdentifier)
            .OrderByDescending(entry => entry.ReceivedAt)
            .ThenByDescending(entry => entry.Id)
            .ToListAsync(cancellationToken);
}
