namespace Nagomi.Api.Features.Audit;

public enum AuditAction
{
    Created,
    Submitted,
    Updated,
    Cancelled,
    Deleted
}

public enum AuditSource
{
    SimulatedUser,
    TransportProvider
}

public sealed class AuditEntry
{
    private readonly List<AuditChange> _changes = [];

    private AuditEntry()
    {
    }

    public AuditEntry(
        Guid id,
        string entityType,
        string entityIdentifier,
        AuditAction action,
        AuditActor actor,
        DateTimeOffset receivedAt,
        IEnumerable<AuditChange> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityIdentifier);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(changes);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("An audit entry identifier is required.", nameof(id));
        }

        Id = id;
        EntityType = entityType;
        EntityIdentifier = entityIdentifier;
        Action = action;
        Source = actor.Source;
        ActorIdentifier = actor.ActorIdentifier;
        ActorDisplayName = actor.ActorDisplayName;
        ProviderIdentifier = actor.ProviderIdentifier;
        ProviderName = actor.ProviderName;
        ReceivedAt = receivedAt;
        _changes.AddRange(changes);
    }

    public Guid Id { get; private set; }

    public string EntityType { get; private set; } = null!;

    public string EntityIdentifier { get; private set; } = null!;

    public AuditAction Action { get; private set; }

    public AuditSource Source { get; private set; }

    public string ActorIdentifier { get; private set; } = null!;

    public string ActorDisplayName { get; private set; } = null!;

    public string? ProviderIdentifier { get; private set; }

    public string? ProviderName { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public IReadOnlyCollection<AuditChange> Changes => _changes.AsReadOnly();
}

public sealed class AuditChange
{
    private AuditChange()
    {
    }

    public AuditChange(
        Guid id,
        string fieldName,
        string? previousValue,
        string? currentValue,
        AuditValueProtection protection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("An audit change identifier is required.", nameof(id));
        }

        if (protection == AuditValueProtection.SensitiveIdentifier &&
            (previousValue is not null || currentValue is not null))
        {
            throw new ArgumentException("Sensitive identifier values cannot be retained.", nameof(protection));
        }

        Id = id;
        FieldName = fieldName;
        PreviousValue = previousValue;
        CurrentValue = currentValue;
        Protection = protection;
    }

    public Guid Id { get; private set; }

    public Guid AuditEntryId { get; private set; }

    public string FieldName { get; private set; } = null!;

    public string? PreviousValue { get; private set; }

    public string? CurrentValue { get; private set; }

    public AuditValueProtection Protection { get; private set; }
}

public enum AuditValueProtection
{
    None,
    MaskedPhone,
    SensitiveIdentifier
}
