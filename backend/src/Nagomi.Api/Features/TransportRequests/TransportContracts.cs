using Nagomi.Api.Domain;

namespace Nagomi.Api.Features.TransportRequests;

public sealed record TransportRequestSnapshot(
    PatientDetails? Patient,
    TransportReasonSnapshot? Reason,
    LocationSnapshot? DefaultOrigin,
    LocationSnapshot? DefaultDestination,
    TransportRequirements? Requirements,
    string? ContractCode,
    Guid? ProviderId,
    string? ProviderName,
    string? ProviderReference,
    string? PrivateNotes,
    string? ProviderVisibleNotes);

public sealed record SubmitOneOffCommand(JourneySchedule Outbound, JourneySchedule? Return);
public sealed record SubmitRecurringCommand(RecurrencePattern Recurrence);
public sealed record UpdateRequestCommand(
    TransportRequestSnapshot Snapshot,
    bool PropagateToJourneys = false,
    bool OverwriteExceptions = false,
    ChangeSource Source = ChangeSource.Nagomi,
    string Actor = "simulated-user");
public sealed record CancelCommand(
    CancellationReason Reason,
    CancellingParty CancellingParty,
    DateTimeOffset? OccurredAt = null,
    ChangeSource Source = ChangeSource.Nagomi,
    string Actor = "simulated-user",
    string? IdempotencyKey = null);
public sealed record RecurrenceChangeCommand(RecurrencePattern Recurrence, bool OverwriteExceptions = false);
public sealed record RecurrenceImpact(
    int Additions,
    int Cancellations,
    int ScheduleChanges,
    int Exceptions,
    int ProtectedJourneys,
    IReadOnlyList<string> AddedOccurrenceKeys,
    IReadOnlyList<string> CancelledJourneyIds);

internal static class TransportMapping
{
    internal static void Apply(this TransportRequestRecord target, TransportRequestSnapshot source, bool provider)
    {
        if (!provider)
        {
            target.Patient = source.Patient;
            target.Reason = source.Reason;
            target.ContractCode = Clean(source.ContractCode);
            target.ProviderId = source.ProviderId;
            target.ProviderName = Clean(source.ProviderName);
            target.PrivateNotes = Clean(source.PrivateNotes);
        }

        target.DefaultOrigin = source.DefaultOrigin?.Copy();
        target.DefaultDestination = source.DefaultDestination?.Copy();
        target.Requirements = (source.Requirements ?? new TransportRequirements()).Copy();
        target.ProviderReference = Clean(source.ProviderReference);
        target.ProviderVisibleNotes = Clean(source.ProviderVisibleNotes);
    }

    internal static TransportRequest ToDomain(this TransportRequestRecord source) => new(
        source.Patient, source.Reason, source.DefaultOrigin, source.DefaultDestination,
        source.Requirements, source.ContractCode, source.ProviderId, source.PrivateNotes,
        source.ProviderVisibleNotes, source.ProviderReference);

    internal static JourneyRecord ToRecord(this Journey source, Guid requestId) => new()
    {
        Id = source.Id,
        TransportRequestId = requestId,
        PublicId = source.PublicId,
        Direction = source.Direction,
        ServiceDate = source.ServiceDate,
        Origin = source.Origin.Copy(),
        Destination = source.Destination.Copy(),
        Requirements = source.Requirements.Copy(),
        Schedule = source.Schedule.Copy(),
        ProviderVisibleNotes = source.ProviderVisibleNotes,
        ProviderReference = source.ProviderReference,
        IsRecurrenceException = source.IsRecurrenceException,
        IsManuallyAdded = source.IsManuallyAdded
    };

    internal static string Key(this JourneyRecord journey) => $"{journey.ServiceDate:yyyy-MM-dd}:{journey.Direction}";
    internal static string Key(this Journey journey) => $"{journey.ServiceDate:yyyy-MM-dd}:{journey.Direction}";
    internal static bool Terminal(this JourneyRecord journey) =>
        journey.CurrentStatus is JourneyStatus.Completed or JourneyStatus.Cancelled;
    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
