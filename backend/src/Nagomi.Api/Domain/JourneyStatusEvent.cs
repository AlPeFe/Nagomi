namespace Nagomi.Api.Domain;

public sealed class JourneyStatusEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid JourneyId { get; private set; }
    public JourneyStatus Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public ChangeSource Source { get; private set; }
    public string Actor { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string? ExternalResourceCode { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public CancellationReason? CancellationReason { get; private set; }
    public CancellingParty? CancellingParty { get; private set; }

    private JourneyStatusEvent()
    {
    }

    internal JourneyStatusEvent(
        Guid journeyId,
        JourneyStatus status,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        ChangeSource source,
        string actor,
        string idempotencyKey,
        string? externalResourceCode,
        decimal? latitude,
        decimal? longitude,
        CancellationReason? cancellationReason,
        CancellingParty? cancellingParty)
    {
        JourneyId = journeyId;
        Status = status;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        Source = source;
        Actor = actor;
        IdempotencyKey = idempotencyKey;
        ExternalResourceCode = externalResourceCode;
        Latitude = latitude;
        Longitude = longitude;
        CancellationReason = cancellationReason;
        CancellingParty = cancellingParty;
    }
}
