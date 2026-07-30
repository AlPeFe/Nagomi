namespace Nagomi.Api.Domain;

public sealed class Journey
{
    private readonly List<JourneyStatusEvent> _statusEvents = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TransportRequestId { get; private set; }
    public string PublicId { get; private set; } = null!;
    public JourneyDirection Direction { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public LocationSnapshot Origin { get; private set; } = null!;
    public LocationSnapshot Destination { get; private set; } = null!;
    public TransportRequirements Requirements { get; private set; } = null!;
    public JourneySchedule Schedule { get; private set; } = null!;
    public string? ProviderVisibleNotes { get; private set; }
    public string? ProviderReference { get; private set; }
    public bool IsRecurrenceException { get; private set; }
    public bool IsManuallyAdded { get; private set; }
    public JourneyStatus CurrentStatus { get; private set; } = JourneyStatus.Scheduled;
    public DateTimeOffset? ActualActivatedAt { get; private set; }
    public DateTimeOffset? ActualArrivedAtOriginAt { get; private set; }
    public DateTimeOffset? ActualPatientPickupAt { get; private set; }
    public DateTimeOffset? ActualArrivedAtDestinationAt { get; private set; }
    public DateTimeOffset? ActualCompletedAt { get; private set; }
    public CancellationReason? CurrentCancellationReason { get; private set; }
    public CancellingParty? CurrentCancellingParty { get; private set; }
    public IReadOnlyCollection<JourneyStatusEvent> StatusEvents => _statusEvents.AsReadOnly();
    public bool IsTerminal => CurrentStatus is JourneyStatus.Completed or JourneyStatus.Cancelled;

    private Journey()
    {
    }

    internal Journey(
        Guid transportRequestId,
        JourneyDirection direction,
        DateOnly serviceDate,
        LocationSnapshot origin,
        LocationSnapshot destination,
        TransportRequirements requirements,
        JourneySchedule schedule,
        string? providerVisibleNotes,
        bool manuallyAdded = false)
    {
        ValidateLocations(origin, destination);
        TransportRequestId = transportRequestId;
        PublicId = NewPublicId("JRN");
        Direction = direction;
        ServiceDate = serviceDate;
        Origin = origin.Copy();
        Destination = destination.Copy();
        Requirements = requirements.Copy();
        Schedule = schedule.Copy();
        ProviderVisibleNotes = Clean(providerVisibleNotes);
        IsManuallyAdded = manuallyAdded;
        IsRecurrenceException = manuallyAdded;
    }

    public void ReplaceOperationalDetails(
        LocationSnapshot origin,
        LocationSnapshot destination,
        TransportRequirements requirements,
        JourneySchedule schedule,
        string? providerVisibleNotes,
        string? providerReference)
    {
        EnsureMutable();
        ValidateLocations(origin, destination);
        Origin = origin.Copy();
        Destination = destination.Copy();
        Requirements = requirements.Copy();
        Schedule = schedule.Copy();
        ProviderVisibleNotes = Clean(providerVisibleNotes);
        ProviderReference = Clean(providerReference);
        IsRecurrenceException = true;
    }

    public void MarkAsRecurrenceException()
    {
        EnsureMutable();
        IsRecurrenceException = true;
    }

    public JourneyStatusEvent AddStatus(
        JourneyStatus status,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        ChangeSource source,
        string actor,
        string idempotencyKey,
        string? externalResourceCode = null,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationReason? cancellationReason = null,
        CancellingParty? cancellingParty = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainValidationException("An idempotency key is required.");
        }

        var prior = _statusEvents.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey.Trim());
        if (prior is not null)
        {
            return prior;
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new DomainValidationException("A status actor is required.");
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainValidationException("Coordinates are outside their valid ranges.");
        }

        if (status == JourneyStatus.Cancelled)
        {
            if (cancellationReason is null || cancellingParty is null)
            {
                throw new DomainValidationException("Cancellation reason and cancelling party are required.");
            }
        }
        else if (cancellationReason.HasValue || cancellingParty.HasValue)
        {
            throw new DomainValidationException("Cancellation metadata is only valid for a cancelled status.");
        }

        if (CurrentStatus == JourneyStatus.Completed && occurredAt >= ActualCompletedAt)
        {
            throw new DomainValidationException("A completed journey cannot be reopened.");
        }

        var statusEvent = new JourneyStatusEvent(
            Id, status, occurredAt, recordedAt, source, actor.Trim(), idempotencyKey.Trim(),
            Clean(externalResourceCode), latitude, longitude, cancellationReason, cancellingParty);
        _statusEvents.Add(statusEvent);
        MaterializeStatus();
        return statusEvent;
    }

    private void MaterializeStatus()
    {
        var current = _statusEvents
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .First();

        CurrentStatus = current.Status;
        CurrentCancellationReason = current.Status == JourneyStatus.Cancelled ? current.CancellationReason : null;
        CurrentCancellingParty = current.Status == JourneyStatus.Cancelled ? current.CancellingParty : null;
        ActualActivatedAt = Latest(JourneyStatus.Activated);
        ActualArrivedAtOriginAt = Latest(JourneyStatus.ArrivedAtOrigin);
        ActualPatientPickupAt = Latest(JourneyStatus.PatientOnBoard);
        ActualArrivedAtDestinationAt = Latest(JourneyStatus.ArrivedAtDestination);
        ActualCompletedAt = Latest(JourneyStatus.Completed);
    }

    private DateTimeOffset? Latest(JourneyStatus status) => _statusEvents
        .Where(x => x.Status == status)
        .Select(x => (DateTimeOffset?)x.OccurredAt)
        .Max();

    private void EnsureMutable()
    {
        if (IsTerminal)
        {
            throw new DomainValidationException("Completed and cancelled journeys cannot be edited.");
        }
    }

    private static void ValidateLocations(LocationSnapshot origin, LocationSnapshot destination)
    {
        if (origin.Type == LocationType.PrivateAddress && destination.Type == LocationType.PrivateAddress)
        {
            throw new DomainValidationException("A private-to-private journey is not allowed.");
        }
    }

    private static string NewPublicId(string prefix) => $"{prefix}-{Guid.NewGuid():N}".ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
