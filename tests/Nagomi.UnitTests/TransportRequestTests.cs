using FluentAssertions;
using Nagomi.Api.Domain;

namespace Nagomi.UnitTests;

public sealed class TransportRequestTests
{
    [Fact]
    public void Draft_allows_missing_patient_and_operational_data()
    {
        var request = new TransportRequest(privateNotes: " internal ");

        request.Status.Should().Be(TransportRequestStatus.Draft);
        request.Patient.Should().BeNull();
        request.PrivateNotes.Should().Be("internal");
        request.CanBePhysicallyDeleted.Should().BeTrue();
        request.Requirements.Mobility.Should().Be(MobilityType.Autonomous);
    }

    [Fact]
    public void Submission_rejects_missing_required_domain_data()
    {
        var request = new TransportRequest();
        var schedule = JourneySchedule.Outbound(Date(2026, 8, 1, 10), true);

        var action = () => request.SubmitOneOff(schedule);

        action.Should().Throw<DomainValidationException>().WithMessage("*reason*");
    }

    [Fact]
    public void One_off_round_trip_assigns_identifiers_and_independent_snapshots()
    {
        var request = ValidRequest();
        var outbound = JourneySchedule.Outbound(Date(2026, 8, 1, 10), true);
        var returning = JourneySchedule.Return(Date(2026, 8, 1, 16));

        request.SubmitOneOff(outbound, returning);

        request.Status.Should().Be(TransportRequestStatus.Active);
        request.PublicId.Should().StartWith("REQ-");
        request.CanBePhysicallyDeleted.Should().BeFalse();
        request.Journeys.Should().HaveCount(2).And.OnlyContain(x => x.PublicId.StartsWith("JRN-"));
        request.Journeys.Select(x => x.Direction).Should().BeEquivalentTo(
            [JourneyDirection.Outbound, JourneyDirection.Return]);
        request.Journeys.Select(x => x.PublicId).Should().OnlyHaveUniqueItems();
        request.Journeys.Should().OnlyContain(x => !ReferenceEquals(x.Requirements, request.Requirements));
    }

    [Fact]
    public void Submission_rejects_private_to_private_route()
    {
        var request = new TransportRequest(
            reason: new TransportReasonSnapshot("R", "Reason"),
            defaultOrigin: Private(),
            defaultDestination: Private());

        var action = () => request.SubmitOneOff(
            JourneySchedule.Outbound(null, false, Date(2026, 8, 1, 9)));

        action.Should().Throw<DomainValidationException>().WithMessage("*private-to-private*");
    }

    [Fact]
    public void Transport_requirements_validate_oxygen_and_copy_to_journeys()
    {
        var requirements = new TransportRequirements(
            MobilityType.Wheelchair, true, 40, 2, companionRequired: true,
            stairsAssistanceRequired: true);
        var request = ValidRequest(requirements);
        request.SubmitOneOff(JourneySchedule.Outbound(Date(2026, 8, 1, 10), true));

        request.Journeys.Single().Requirements.Should().BeEquivalentTo(requirements);
        var invalid = () => new TransportRequirements(requiresOxygen: true);
        invalid.Should().Throw<DomainValidationException>().WithMessage("*Oxygen*");
    }

    private static TransportRequest ValidRequest(TransportRequirements? requirements = null) => new(
        patient: new PatientDetails(firstName: "Ana", phone: "555"),
        reason: new TransportReasonSnapshot("CONS", "Consultation"),
        defaultOrigin: Private(),
        defaultDestination: Facility(),
        requirements: requirements,
        contractCode: "C-1",
        providerVisibleNotes: "Ring bell");

    internal static LocationSnapshot Private() => new(LocationType.PrivateAddress, street: "Main");
    internal static LocationSnapshot Facility() => new(LocationType.HealthcareFacility, "Hospital");
    internal static DateTimeOffset Date(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
