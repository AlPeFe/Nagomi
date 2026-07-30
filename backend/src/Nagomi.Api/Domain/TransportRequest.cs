namespace Nagomi.Api.Domain;

public sealed class TransportRequest
{
    private readonly List<Journey> _journeys = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string? PublicId { get; private set; }
    public TransportRequestStatus Status { get; private set; } = TransportRequestStatus.Draft;
    public PatientDetails? Patient { get; private set; }
    public TransportReasonSnapshot? Reason { get; private set; }
    public LocationSnapshot? DefaultOrigin { get; private set; }
    public LocationSnapshot? DefaultDestination { get; private set; }
    public TransportRequirements Requirements { get; private set; } = new();
    public string? ContractCode { get; private set; }
    public Guid? ProviderId { get; private set; }
    public string? ProviderReference { get; private set; }
    public string? PrivateNotes { get; private set; }
    public string? ProviderVisibleNotes { get; private set; }
    public RecurrencePattern? Recurrence { get; private set; }
    public IReadOnlyCollection<Journey> Journeys => _journeys.AsReadOnly();
    public bool CanBePhysicallyDeleted => Status == TransportRequestStatus.Draft;

    private TransportRequest()
    {
    }

    public TransportRequest(
        PatientDetails? patient = null,
        TransportReasonSnapshot? reason = null,
        LocationSnapshot? defaultOrigin = null,
        LocationSnapshot? defaultDestination = null,
        TransportRequirements? requirements = null,
        string? contractCode = null,
        Guid? providerId = null,
        string? privateNotes = null,
        string? providerVisibleNotes = null,
        string? providerReference = null)
    {
        Patient = patient;
        Reason = reason;
        DefaultOrigin = defaultOrigin?.Copy();
        DefaultDestination = defaultDestination?.Copy();
        Requirements = (requirements ?? new TransportRequirements()).Copy();
        ContractCode = Clean(contractCode);
        ProviderId = providerId;
        PrivateNotes = Clean(privateNotes);
        ProviderVisibleNotes = Clean(providerVisibleNotes);
        ProviderReference = Clean(providerReference);
    }

    public void SubmitOneOff(JourneySchedule outboundSchedule, JourneySchedule? returnSchedule = null)
    {
        EnsureDraftAndValid();
        if (returnSchedule is not null)
        {
            JourneySchedule.ValidateRoundTrip(outboundSchedule, returnSchedule);
        }

        Activate();
        var serviceDate = DateOnly.FromDateTime(outboundSchedule.ScheduledStartAt.Date);
        AddGeneratedJourney(JourneyDirection.Outbound, serviceDate, DefaultOrigin!, DefaultDestination!, outboundSchedule);

        if (returnSchedule is not null)
        {
            AddGeneratedJourney(
                JourneyDirection.Return,
                DateOnly.FromDateTime(returnSchedule.ScheduledStartAt.Date),
                DefaultDestination!,
                DefaultOrigin!,
                returnSchedule);
        }
    }

    public void SubmitRecurring(RecurrencePattern recurrence)
    {
        EnsureDraftAndValid();
        Recurrence = recurrence ?? throw new DomainValidationException("A recurrence pattern is required.");
        Activate();
        GenerateRecurrenceJourneys();
    }

    public int GenerateRecurrenceJourneys()
    {
        if (Status == TransportRequestStatus.Draft || Recurrence is null)
        {
            throw new DomainValidationException("An active recurring request is required.");
        }

        var added = 0;
        foreach (var (date, weekday) in Recurrence.Occurrences())
        {
            var appointment = Recurrence.At(date, weekday.OutboundAppointmentTime);
            var start = weekday.OutboundStartTime.HasValue
                ? Recurrence.At(date, weekday.OutboundStartTime.Value)
                : (DateTimeOffset?)null;
            var pickup = weekday.OutboundPickupTime.HasValue
                ? Recurrence.At(date, weekday.OutboundPickupTime.Value)
                : (DateTimeOffset?)null;
            var outbound = JourneySchedule.Outbound(
                appointment,
                DefaultDestination!.Type == LocationType.HealthcareFacility,
                start,
                pickup);

            if (TryAddGeneratedJourney(JourneyDirection.Outbound, date, DefaultOrigin!, DefaultDestination, outbound))
            {
                added++;
            }

            if (weekday.ReturnPickupTime.HasValue)
            {
                var returnDate = weekday.ReturnPickupNextDay ? date.AddDays(1) : date;
                var returnSchedule = JourneySchedule.Return(
                    Recurrence.At(returnDate, weekday.ReturnPickupTime.Value),
                    weekday.ReturnPickupTimePending);
                JourneySchedule.ValidateRoundTrip(outbound, returnSchedule);

                if (TryAddGeneratedJourney(JourneyDirection.Return, date, DefaultDestination, DefaultOrigin!, returnSchedule))
                {
                    added++;
                }
            }
        }

        return added;
    }

    public Journey AddExceptionalJourney(
        JourneyDirection direction,
        DateOnly serviceDate,
        LocationSnapshot origin,
        LocationSnapshot destination,
        JourneySchedule schedule)
    {
        if (Status != TransportRequestStatus.Active)
        {
            throw new DomainValidationException("Exceptional journeys can only be added to active requests.");
        }

        if (_journeys.Any(x => x.ServiceDate == serviceDate && x.Direction == direction && !x.IsTerminal))
        {
            throw new DomainValidationException("An active journey already exists for that date and direction.");
        }

        var journey = new Journey(
            Id, direction, serviceDate, origin, destination, Requirements, schedule,
            ProviderVisibleNotes, manuallyAdded: true);
        _journeys.Add(journey);
        return journey;
    }

    private void EnsureDraftAndValid()
    {
        if (Status != TransportRequestStatus.Draft)
        {
            throw new DomainValidationException("A submitted request cannot be submitted again or returned to draft.");
        }

        if (Reason is null)
        {
            throw new DomainValidationException("A transport reason is required.");
        }

        if (DefaultOrigin is null || DefaultDestination is null)
        {
            throw new DomainValidationException("Default origin and destination are required.");
        }

        if (DefaultOrigin.Type == LocationType.PrivateAddress && DefaultDestination.Type == LocationType.PrivateAddress)
        {
            throw new DomainValidationException("A private-to-private journey is not allowed.");
        }
    }

    private void Activate()
    {
        PublicId = $"REQ-{Guid.NewGuid():N}".ToUpperInvariant();
        Status = TransportRequestStatus.Active;
    }

    private void AddGeneratedJourney(
        JourneyDirection direction,
        DateOnly date,
        LocationSnapshot origin,
        LocationSnapshot destination,
        JourneySchedule schedule) =>
        _journeys.Add(new Journey(
            Id, direction, date, origin, destination, Requirements, schedule, ProviderVisibleNotes));

    private bool TryAddGeneratedJourney(
        JourneyDirection direction,
        DateOnly date,
        LocationSnapshot origin,
        LocationSnapshot destination,
        JourneySchedule schedule)
    {
        if (_journeys.Any(x => x.ServiceDate == date && x.Direction == direction))
        {
            return false;
        }

        AddGeneratedJourney(direction, date, origin, destination, schedule);
        return true;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
