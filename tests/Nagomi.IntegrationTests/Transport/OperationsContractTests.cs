using FluentAssertions;
using Nagomi.Api.Features.Operations;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.IntegrationTests.Transport;

public sealed class OperationsContractTests
{
    [Fact]
    public void Journey_list_contract_excludes_sensitive_identifiers()
    {
        var properties = typeof(JourneyOperationsRow).GetProperties().Select(x => x.Name);

        properties.Should().NotContain("DocumentNumber");
        properties.Should().NotContain("HealthCardNumber");
        properties.Should().Contain(["PatientName", "PatientPhone", "RequestPublicId", "JourneyPublicId"]);
    }

    [Fact]
    public void Persistence_boundary_exposes_one_save_operation_and_queryable_aggregates()
    {
        typeof(ITransportDb).GetProperty(nameof(ITransportDb.TransportRequests))!.PropertyType
            .Should().Be<IQueryable<TransportRequestRecord>>();
        typeof(ITransportDb).GetProperty(nameof(ITransportDb.Journeys))!.PropertyType
            .Should().Be<IQueryable<JourneyRecord>>();
        typeof(ITransportDb).GetMethods().Count(x => x.Name == nameof(ITransportDb.SaveChangesAsync))
            .Should().Be(1);
    }
}
