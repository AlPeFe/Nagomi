using FluentAssertions;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.EmergencyTransports;

namespace Nagomi.UnitTests;

public sealed class EmergencyTransportTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_assigns_public_identifier_and_minimal_fields()
    {
        var record = EmergencyTransportRecord.Create(
            "Atropello en vía pública",
            new IncidentLocation(41.3874m, 2.1686m, "Carrer de Balmes 1", "Barcelona"),
            "600111222",
            "Acceso por la entrada principal",
            Now);

        record.PublicId.Should().StartWith("EMG-");
        record.Status.Should().Be(EmergencyTransportStatus.Active);
        record.Reason.Should().Be("Atropello en vía pública");
        record.Incident.Latitude.Should().Be(41.3874m);
        record.Incident.Longitude.Should().Be(2.1686m);
        record.Incident.Address.Should().Be("Carrer de Balmes 1");
        record.Incident.Municipality.Should().Be("Barcelona");
        record.ContactPhone.Should().Be("600111222");
        record.Observations.Should().Be("Acceso por la entrada principal");
        record.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_rejects_missing_reason()
    {
        var act = () => EmergencyTransportRecord.Create(
            "   ", new IncidentLocation(41.3874m, 2.1686m), null, null, Now);

        act.Should().Throw<DomainValidationException>().WithMessage("*reason*");
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void Incident_rejects_coordinates_outside_valid_ranges(decimal latitude, decimal longitude)
    {
        var act = () => new IncidentLocation(latitude, longitude);

        act.Should().Throw<DomainValidationException>().WithMessage("*coordinates*");
    }

    [Fact]
    public void Optional_text_fields_are_trimmed()
    {
        var record = EmergencyTransportRecord.Create(
            "Caída", new IncidentLocation(41.38m, 2.17m, "  Calle Mayor 5  "), " 600111222 ", "  ", Now);

        record.Incident.Address.Should().Be("Calle Mayor 5");
        record.ContactPhone.Should().Be("600111222");
        record.Observations.Should().BeNull();
    }
}
