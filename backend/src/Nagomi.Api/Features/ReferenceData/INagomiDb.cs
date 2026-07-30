using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ReferenceData;

// Persistence surface the application DbContext is expected to implement.
public interface INagomiDb
{
    DbSet<IneAutonomousCommunity> IneAutonomousCommunities { get; }
    DbSet<IneProvince> IneProvinces { get; }
    DbSet<IneMunicipality> IneMunicipalities { get; }
    DbSet<TransportReason> TransportReasons { get; }
    DbSet<HealthcareFacility> HealthcareFacilities { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
