using FluentAssertions;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.Journeys;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.IntegrationTests.Transport;

public sealed class JourneyMaterializerTests
{
    [Fact]
    public void Delayed_status_is_retained_without_replacing_current_status()
    {
        var journey = Journey();
        journey.StatusHistory.Add(Status(JourneyStatus.PatientOnBoard, At(11), At(11, 1), "newer"));
        journey.StatusHistory.Add(Status(JourneyStatus.ArrivedAtOrigin, At(10), At(12), "delayed"));

        JourneyMaterializer.Apply(journey);

        journey.CurrentStatus.Should().Be(JourneyStatus.PatientOnBoard);
        journey.ActualArrivedAtOriginAt.Should().Be(At(10));
        journey.ActualPatientPickupAt.Should().Be(At(11));
    }

    [Fact]
    public void Later_status_reopens_cancelled_journey_and_clears_cancellation_metadata()
    {
        var journey = Journey();
        journey.StatusHistory.Add(Status(JourneyStatus.Cancelled, At(10), At(10, 1), "cancel",
            CancellationReason.ProviderUnavailable, CancellingParty.TransportProvider));
        journey.StatusHistory.Add(Status(JourneyStatus.Activated, At(11), At(11, 1), "reopen"));

        JourneyMaterializer.Apply(journey);

        journey.CurrentStatus.Should().Be(JourneyStatus.Activated);
        journey.CurrentCancellationReason.Should().BeNull();
        journey.CurrentCancellingParty.Should().BeNull();
    }

    [Fact]
    public void Latest_matching_events_materialize_actual_times()
    {
        var journey = Journey();
        journey.StatusHistory.Add(Status(JourneyStatus.ArrivedAtDestination, At(10), At(10, 1), "first"));
        journey.StatusHistory.Add(Status(JourneyStatus.ArrivedAtDestination, At(10, 5), At(10, 6), "second"));
        journey.StatusHistory.Add(Status(JourneyStatus.Completed, At(10, 10), At(10, 11), "complete"));

        JourneyMaterializer.Apply(journey);

        journey.CurrentStatus.Should().Be(JourneyStatus.Completed);
        journey.ActualArrivedAtDestinationAt.Should().Be(At(10, 5));
        journey.ActualCompletedAt.Should().Be(At(10, 10));
    }

    private static JourneyRecord Journey() => new()
    {
        PublicId = "JRN-TEST",
        Origin = new LocationSnapshot(LocationType.PrivateAddress, street: "Main"),
        Destination = new LocationSnapshot(LocationType.HealthcareFacility, "Hospital"),
        Schedule = JourneySchedule.Outbound(At(12), true)
    };

    private static JourneyStatusRecord Status(
        JourneyStatus status, DateTimeOffset occurredAt, DateTimeOffset recordedAt, string key,
        CancellationReason? reason = null, CancellingParty? party = null) => new()
    {
        Status = status,
        OccurredAt = occurredAt,
        RecordedAt = recordedAt,
        Source = ChangeSource.TransportProvider,
        Actor = "provider",
        IdempotencyKey = key,
        CancellationReason = reason,
        CancellingParty = party
    };

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 8, 1, hour, minute, 0, TimeSpan.Zero);
}
