using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ReferenceData;

public interface ITransportReasonLookup
{
    Task<TransportReasonSnapshot?> FindActiveSnapshotAsync(
        Guid reasonId,
        CancellationToken cancellationToken = default);
}

public sealed class TransportReasonLookup(INagomiDb db) : ITransportReasonLookup
{
    public Task<TransportReasonSnapshot?> FindActiveSnapshotAsync(
        Guid reasonId,
        CancellationToken cancellationToken = default) =>
        db.TransportReasons.AsNoTracking()
            .Where(x => x.Id == reasonId && x.IsActive)
            .Select(x => new TransportReasonSnapshot(x.Code, x.Description))
            .SingleOrDefaultAsync(cancellationToken);
}
