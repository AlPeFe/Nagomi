namespace Nagomi.Api.Features.Audit;

public sealed class AuditRecorder(IAuditDiffService diffService)
{
    public AuditEntry RecordSnapshotChange(
        string entityType,
        string entityIdentifier,
        AuditAction action,
        AuditActor actor,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, object?> previousSnapshot,
        IReadOnlyDictionary<string, object?> currentSnapshot) =>
        new(
            Guid.NewGuid(),
            entityType,
            entityIdentifier,
            action,
            actor,
            receivedAt,
            diffService.Compare(previousSnapshot, currentSnapshot));
}
