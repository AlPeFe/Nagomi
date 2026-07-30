using FluentAssertions;
using Nagomi.Api.Features.Audit;

namespace Nagomi.UnitTests.Audit;

public sealed class AuditEndpointsTests
{
    [Fact]
    public async Task GetHistoryAsync_ReturnsUserVisibleHistoryFromAuditQueryOnly()
    {
        var entry = new AuditEntry(
            Guid.NewGuid(),
            "Journey",
            "JRN-8",
            AuditAction.Updated,
            AuditActor.ForProvider("client-4", "Integration client", "provider-9", "Provider Nine"),
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            [new AuditChange(Guid.NewGuid(), "Notes", "before", "after", AuditValueProtection.None)]);
        var query = new StubAuditHistoryQuery([entry]);

        var result = await AuditEndpoints.GetHistoryAsync(
            "Journey", "JRN-8", query, CancellationToken.None);

        result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            EntityType = "Journey",
            EntityIdentifier = "JRN-8",
            Source = AuditSource.TransportProvider,
            ActorIdentifier = "client-4",
            ProviderIdentifier = "provider-9"
        });
        query.ReceivedEntityType.Should().Be("Journey");
        query.ReceivedEntityIdentifier.Should().Be("JRN-8");
    }

    [Fact]
    public void ToResponse_DoesNotResolveActorAgainstCurrentCredentials()
    {
        var entry = new AuditEntry(
            Guid.NewGuid(),
            "TransportRequest",
            "REQ-2",
            AuditAction.Cancelled,
            AuditActor.ForProvider("revoked-client", "Old client", "old-provider", "Old Provider"),
            DateTimeOffset.UtcNow,
            []);

        var response = AuditEndpoints.ToResponse(entry);

        response.ActorIdentifier.Should().Be("revoked-client");
        response.ProviderIdentifier.Should().Be("old-provider");
        response.ProviderName.Should().Be("Old Provider");
    }

    private sealed class StubAuditHistoryQuery(IReadOnlyList<AuditEntry> entries) : IAuditHistoryQuery
    {
        public string? ReceivedEntityType { get; private set; }

        public string? ReceivedEntityIdentifier { get; private set; }

        public Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(
            string entityType,
            string entityIdentifier,
            CancellationToken cancellationToken)
        {
            ReceivedEntityType = entityType;
            ReceivedEntityIdentifier = entityIdentifier;
            return Task.FromResult(entries);
        }
    }
}
