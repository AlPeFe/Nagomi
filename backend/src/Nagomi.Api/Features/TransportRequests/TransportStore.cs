using Nagomi.Api.Domain;

namespace Nagomi.Api.Features.TransportRequests;

public interface ITransportDb
{
    IQueryable<TransportRequestRecord> TransportRequests { get; }
    IQueryable<JourneyRecord> Journeys { get; }
    IQueryable<TransportAuditRecord> TransportAudit { get; }

    void Add(TransportRequestRecord request);
    void Add(JourneyRecord journey);
    void Add(JourneyStatusRecord status);
    void Add(TransportAuditRecord audit);
    void Remove(TransportRequestRecord request);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class TransportRequestRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? PublicId { get; set; }
    public TransportRequestStatus Status { get; set; } = TransportRequestStatus.Draft;
    public PatientDetails? Patient { get; set; }
    public TransportReasonSnapshot? Reason { get; set; }
    public LocationSnapshot? DefaultOrigin { get; set; }
    public LocationSnapshot? DefaultDestination { get; set; }
    public TransportRequirements Requirements { get; set; } = new();
    public string? ContractCode { get; set; }
    public Guid? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderReference { get; set; }
    public string? PrivateNotes { get; set; }
    public string? ProviderVisibleNotes { get; set; }
    public RecurrencePattern? Recurrence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<JourneyRecord> JourneyRecords { get; set; } = [];
}

public sealed class JourneyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransportRequestId { get; set; }
    public string PublicId { get; set; } = null!;
    public JourneyDirection Direction { get; set; }
    public DateOnly ServiceDate { get; set; }
    public LocationSnapshot Origin { get; set; } = null!;
    public LocationSnapshot Destination { get; set; } = null!;
    public TransportRequirements Requirements { get; set; } = new();
    public JourneySchedule Schedule { get; set; } = null!;
    public string? ProviderVisibleNotes { get; set; }
    public string? ProviderReference { get; set; }
    public bool IsRecurrenceException { get; set; }
    public bool IsManuallyAdded { get; set; }
    public JourneyStatus CurrentStatus { get; set; } = JourneyStatus.Scheduled;
    public DateTimeOffset? ActualActivatedAt { get; set; }
    public DateTimeOffset? ActualArrivedAtOriginAt { get; set; }
    public DateTimeOffset? ActualPatientPickupAt { get; set; }
    public DateTimeOffset? ActualArrivedAtDestinationAt { get; set; }
    public DateTimeOffset? ActualCompletedAt { get; set; }
    public CancellationReason? CurrentCancellationReason { get; set; }
    public CancellingParty? CurrentCancellingParty { get; set; }
    public bool ExternallyModified { get; set; }
    public string? RetrievalState { get; set; }
    public List<JourneyStatusRecord> StatusHistory { get; set; } = [];
}

public sealed class JourneyStatusRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JourneyId { get; set; }
    public JourneyStatus Status { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public ChangeSource Source { get; set; }
    public string Actor { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public string? ExternalResourceCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public CancellationReason? CancellationReason { get; set; }
    public CancellingParty? CancellingParty { get; set; }
}

public sealed class TransportAuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = null!;
    public string EntityIdentifier { get; set; } = null!;
    public string Action { get; set; } = null!;
    public ChangeSource Source { get; set; }
    public string Actor { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; }
}
