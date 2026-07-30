using FluentAssertions;
using Nagomi.Api.Domain;

namespace Nagomi.UnitTests;

public sealed class SchedulingTests
{
    [Fact]
    public void Outbound_defaults_start_to_one_hour_before_appointment()
    {
        var appointment = TransportRequestTests.Date(2026, 8, 1, 10);

        var schedule = JourneySchedule.Outbound(appointment, true);

        schedule.ScheduledStartAt.Should().Be(appointment.AddHours(-1));
    }

    [Fact]
    public void Facility_destination_requires_appointment()
    {
        var action = () => JourneySchedule.Outbound(
            null, true, TransportRequestTests.Date(2026, 8, 1, 9));

        action.Should().Throw<DomainValidationException>().WithMessage("*appointment*");
    }

    [Fact]
    public void Pending_return_requires_2359_placeholder()
    {
        var valid = JourneySchedule.Return(TransportRequestTests.Date(2026, 8, 1, 23, 59), true);
        var invalid = () => JourneySchedule.Return(TransportRequestTests.Date(2026, 8, 1, 23, 58), true);

        valid.PickupTimePending.Should().BeTrue();
        invalid.Should().Throw<DomainValidationException>().WithMessage("*23:59*");
    }

    [Fact]
    public void Overnight_round_trip_within_24_hours_is_valid()
    {
        var outbound = JourneySchedule.Outbound(
            TransportRequestTests.Date(2026, 8, 1, 23), true,
            TransportRequestTests.Date(2026, 8, 1, 22));
        var returning = JourneySchedule.Return(TransportRequestTests.Date(2026, 8, 2, 1, 30));

        var action = () => JourneySchedule.ValidateRoundTrip(outbound, returning);

        action.Should().NotThrow();
    }

    [Fact]
    public void Round_trip_over_24_hours_is_rejected()
    {
        var outbound = JourneySchedule.Outbound(
            TransportRequestTests.Date(2026, 8, 1, 10), true,
            TransportRequestTests.Date(2026, 8, 1, 9));
        var returning = JourneySchedule.Return(TransportRequestTests.Date(2026, 8, 2, 9, 1));

        var action = () => JourneySchedule.ValidateRoundTrip(outbound, returning);

        action.Should().Throw<DomainValidationException>().WithMessage("*24 hours*");
    }
}
