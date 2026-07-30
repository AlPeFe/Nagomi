using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Features.ProviderIntegration;

namespace Nagomi.UnitTests.ProviderIntegration;

public sealed class NotificationContractTests
{
    [Fact]
    public void NotificationMessage_SerializesOnlyMinimalRoutingMetadata()
    {
        var message = new ProviderNotificationMessage(
            Guid.NewGuid(), "request.submitted", "REQ-1", "CONTRACT-A",
            DateTimeOffset.Parse("2026-07-29T12:00:00Z"),
            "/api/provider/requests/REQ-1?messageId=1");

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var properties = document.RootElement.EnumerateObject().Select(x => x.Name).ToArray();

        properties.Should().BeEquivalentTo(
            "messageId", "messageType", "entityPublicId", "contractCode", "timestamp", "retrievalUrl");
        properties.Should().NotContain(x =>
            x.Contains("patient", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("address", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("requirement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Worker_AllowsFiveRetriesAfterInitialFailure()
    {
        ProviderOutboxWorker.MaximumRetries.Should().Be(5);
    }

    [Fact]
    public void IdempotencyHash_IgnoresJsonObjectPropertyOrder()
    {
        ProviderCommandIdempotency.HashCanonicalJson("{\"a\":1,\"b\":2}")
            .Should().Be(ProviderCommandIdempotency.HashCanonicalJson("{ \"b\": 2, \"a\": 1 }"));
    }
}
