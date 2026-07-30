using FluentAssertions;
using Nagomi.Api.Domain;

namespace Nagomi.UnitTests;

public sealed class RecurrenceTests
{
    [Fact]
    public void Pattern_requires_weekdays_and_has_six_month_maximum()
    {
        var empty = () => new RecurrencePattern(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), []);
        var tooLong = () => new RecurrencePattern(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 2),
            [new WeekdaySchedule(DayOfWeek.Monday, new TimeOnly(10, 0))]);

        empty.Should().Throw<DomainValidationException>().WithMessage("*weekday*");
        tooLong.Should().Throw<DomainValidationException>().WithMessage("*six months*");
    }

    [Fact]
    public void Generation_includes_boundaries_and_creates_per_weekday_round_trips()
    {
        var request = ValidRequest();
        var pattern = new RecurrencePattern(
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 10),
            [new WeekdaySchedule(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(15, 0))]);

        request.SubmitRecurring(pattern);

        request.Journeys.Should().HaveCount(4);
        request.Journeys.Select(x => x.ServiceDate).Distinct().Should().BeEquivalentTo(
            [new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 10)]);
        request.Journeys.GroupBy(x => x.ServiceDate).Should().OnlyContain(x => x.Count() == 2);
    }

    [Fact]
    public void Regeneration_is_idempotent()
    {
        var request = ValidRequest();
        request.SubmitRecurring(new RecurrencePattern(
            new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3),
            [new WeekdaySchedule(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(15, 0))]));

        var added = request.GenerateRecurrenceJourneys();

        added.Should().Be(0);
        request.Journeys.Should().HaveCount(2);
    }

    [Fact]
    public void Individually_edited_and_manual_journeys_are_whole_journey_exceptions()
    {
        var request = ValidRequest();
        request.SubmitRecurring(new RecurrencePattern(
            new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3),
            [new WeekdaySchedule(DayOfWeek.Monday, new TimeOnly(10, 0))]));
        var generated = request.Journeys.Single();

        generated.ReplaceOperationalDetails(
            TransportRequestTests.Private(), TransportRequestTests.Facility(),
            new TransportRequirements(MobilityType.Stretcher),
            JourneySchedule.Outbound(TransportRequestTests.Date(2026, 8, 3, 11), true),
            "changed", "provider-1");
        var manual = request.AddExceptionalJourney(
            JourneyDirection.Outbound, new DateOnly(2026, 8, 4),
            TransportRequestTests.Private(), TransportRequestTests.Facility(),
            JourneySchedule.Outbound(TransportRequestTests.Date(2026, 8, 4, 10), true));

        generated.IsRecurrenceException.Should().BeTrue();
        generated.Requirements.Mobility.Should().Be(MobilityType.Stretcher);
        manual.IsRecurrenceException.Should().BeTrue();
        manual.IsManuallyAdded.Should().BeTrue();
    }

    [Fact]
    public void Manual_addition_enforces_one_active_direction_per_date()
    {
        var request = ValidRequest();
        request.SubmitRecurring(new RecurrencePattern(
            new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3),
            [new WeekdaySchedule(DayOfWeek.Monday, new TimeOnly(10, 0))]));

        var action = () => request.AddExceptionalJourney(
            JourneyDirection.Outbound, new DateOnly(2026, 8, 3),
            TransportRequestTests.Private(), TransportRequestTests.Facility(),
            JourneySchedule.Outbound(TransportRequestTests.Date(2026, 8, 3, 12), true));

        action.Should().Throw<DomainValidationException>().WithMessage("*already exists*");
    }

    private static TransportRequest ValidRequest() => new(
        reason: new TransportReasonSnapshot("R", "Reason"),
        defaultOrigin: TransportRequestTests.Private(),
        defaultDestination: TransportRequestTests.Facility());
}
