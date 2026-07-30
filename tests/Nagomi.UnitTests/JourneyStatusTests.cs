using FluentAssertions;
using Nagomi.Api.Domain;

namespace Nagomi.UnitTests;

public sealed class JourneyStatusTests
{
    [Fact]
    public void Status_event_retains_all_metadata()
    {
        var journey = Journey();
        var occurred = At(10);
        var recorded = At(10, 5);

        var statusEvent = journey.AddStatus(
            JourneyStatus.Activated, occurred, recorded, ChangeSource.TransportProvider,
            "client-1", "key-1", "AMB-7", 40.4m, -3.7m);

        statusEvent.Should().BeEquivalentTo(new
        {
            Status = JourneyStatus.Activated,
            OccurredAt = occurred,
            RecordedAt = recorded,
            Source = ChangeSource.TransportProvider,
            Actor = "client-1",
            IdempotencyKey = "key-1",
            ExternalResourceCode = "AMB-7",
            Latitude = (decimal?)40.4m,
            Longitude = (decimal?)-3.7m
        });
    }

    [Fact]
    public void Delayed_event_is_retained_without_replacing_newer_current_status()
    {
        var journey = Journey();
        journey.AddStatus(JourneyStatus.PatientOnBoard, At(11), At(11, 1),
            ChangeSource.TransportProvider, "provider", "newer");

        journey.AddStatus(JourneyStatus.ArrivedAtOrigin, At(10), At(12),
            ChangeSource.TransportProvider, "provider", "older");

        journey.StatusEvents.Should().HaveCount(2);
        journey.CurrentStatus.Should().Be(JourneyStatus.PatientOnBoard);
    }

    [Fact]
    public void Same_idempotency_key_returns_prior_event_without_duplicate()
    {
        var journey = Journey();
        var first = journey.AddStatus(JourneyStatus.Activated, At(10), At(10, 1),
            ChangeSource.TransportProvider, "provider", "same");

        var repeated = journey.AddStatus(JourneyStatus.Completed, At(12), At(12, 1),
            ChangeSource.TransportProvider, "provider", "same");

        repeated.Should().BeSameAs(first);
        journey.StatusEvents.Should().ContainSingle();
        journey.CurrentStatus.Should().Be(JourneyStatus.Activated);
    }

    [Fact]
    public void Latest_corresponding_events_materialize_actual_timestamps()
    {
        var journey = Journey();
        journey.AddStatus(JourneyStatus.Activated, At(9), At(9, 1), ChangeSource.TransportProvider, "p", "1");
        journey.AddStatus(JourneyStatus.ArrivedAtOrigin, At(9, 20), At(9, 21), ChangeSource.TransportProvider, "p", "2");
        journey.AddStatus(JourneyStatus.PatientOnBoard, At(9, 30), At(9, 31), ChangeSource.TransportProvider, "p", "3");
        journey.AddStatus(JourneyStatus.ArrivedAtDestination, At(10), At(10, 1), ChangeSource.TransportProvider, "p", "4");
        journey.AddStatus(JourneyStatus.ArrivedAtDestination, At(10, 5), At(10, 6), ChangeSource.TransportProvider, "p", "5");
        journey.AddStatus(JourneyStatus.Completed, At(10, 10), At(10, 11), ChangeSource.TransportProvider, "p", "6");

        journey.ActualActivatedAt.Should().Be(At(9));
        journey.ActualArrivedAtOriginAt.Should().Be(At(9, 20));
        journey.ActualPatientPickupAt.Should().Be(At(9, 30));
        journey.ActualArrivedAtDestinationAt.Should().Be(At(10, 5));
        journey.ActualCompletedAt.Should().Be(At(10, 10));
    }

    [Fact]
    public void Cancellation_requires_metadata_and_later_event_reopens_journey()
    {
        var journey = Journey();
        var invalid = () => journey.AddStatus(
            JourneyStatus.Cancelled, At(10), At(10, 1), ChangeSource.TransportProvider,
            "provider", "cancel-without-reason");
        invalid.Should().Throw<DomainValidationException>().WithMessage("*reason*");

        journey.AddStatus(
            JourneyStatus.Cancelled, At(10), At(10, 1), ChangeSource.TransportProvider,
            "provider", "cancel", cancellationReason: CancellationReason.ProviderUnavailable,
            cancellingParty: CancellingParty.TransportProvider);
        journey.CurrentCancellationReason.Should().Be(CancellationReason.ProviderUnavailable);

        journey.AddStatus(JourneyStatus.Activated, At(11), At(11, 1),
            ChangeSource.TransportProvider, "provider", "reopen");

        journey.CurrentStatus.Should().Be(JourneyStatus.Activated);
        journey.CurrentCancellationReason.Should().BeNull();
        journey.CurrentCancellingParty.Should().BeNull();
    }

    [Fact]
    public void Completed_is_terminal_even_when_intermediate_statuses_were_skipped()
    {
        var journey = Journey();
        journey.AddStatus(JourneyStatus.Completed, At(10), At(10, 1),
            ChangeSource.TransportProvider, "provider", "complete");

        var action = () => journey.AddStatus(JourneyStatus.Activated, At(11), At(11, 1),
            ChangeSource.TransportProvider, "provider", "reopen-attempt");

        journey.CurrentStatus.Should().Be(JourneyStatus.Completed);
        action.Should().Throw<DomainValidationException>().WithMessage("*completed*");
    }

    [Fact]
    public void Completed_journey_retains_older_delayed_event_without_reopening()
    {
        var journey = Journey();
        journey.AddStatus(JourneyStatus.Completed, At(10), At(10, 1),
            ChangeSource.TransportProvider, "provider", "complete");

        journey.AddStatus(JourneyStatus.ArrivedAtDestination, At(9, 50), At(11),
            ChangeSource.TransportProvider, "provider", "delayed");

        journey.StatusEvents.Should().HaveCount(2);
        journey.CurrentStatus.Should().Be(JourneyStatus.Completed);
        journey.ActualArrivedAtDestinationAt.Should().Be(At(9, 50));
    }

    private static Journey Journey()
    {
        var request = new TransportRequest(
            reason: new TransportReasonSnapshot("R", "Reason"),
            defaultOrigin: TransportRequestTests.Private(),
            defaultDestination: TransportRequestTests.Facility());
        request.SubmitOneOff(JourneySchedule.Outbound(At(12), true));
        return request.Journeys.Single();
    }

    private static DateTimeOffset At(int hour, int minute = 0) =>
        TransportRequestTests.Date(2026, 8, 1, hour, minute);
}
