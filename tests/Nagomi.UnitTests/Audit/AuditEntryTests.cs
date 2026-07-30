using FluentAssertions;
using Nagomi.Api.Features.Audit;

namespace Nagomi.UnitTests.Audit;

public sealed class AuditEntryTests
{
    [Fact]
    public void Constructor_SnapshotsSimulatedUserAttributionAndReceiptTime()
    {
        var receivedAt = new DateTimeOffset(2026, 7, 29, 10, 15, 0, TimeSpan.Zero);
        var actor = AuditActor.ForSimulatedUser("user-17", "Nagomi Operator");

        var entry = new AuditEntry(
            Guid.NewGuid(), "TransportRequest", "REQ-42", AuditAction.Submitted,
            actor, receivedAt, []);

        entry.Source.Should().Be(AuditSource.SimulatedUser);
        entry.ActorIdentifier.Should().Be("user-17");
        entry.ActorDisplayName.Should().Be("Nagomi Operator");
        entry.ProviderIdentifier.Should().BeNull();
        entry.ReceivedAt.Should().Be(receivedAt);
    }

    [Fact]
    public void Constructor_SnapshotsProviderAndClientAttributionWithoutCredentialReference()
    {
        var actor = AuditActor.ForProvider(
            "oauth-client-revoked", "Provider API client", "provider-9", "Ambulancias Norte");

        var entry = new AuditEntry(
            Guid.NewGuid(), "Journey", "JRN-8", AuditAction.Updated,
            actor, DateTimeOffset.UtcNow, []);

        entry.Source.Should().Be(AuditSource.TransportProvider);
        entry.ActorIdentifier.Should().Be("oauth-client-revoked");
        entry.ActorDisplayName.Should().Be("Provider API client");
        entry.ProviderIdentifier.Should().Be("provider-9");
        entry.ProviderName.Should().Be("Ambulancias Norte");
    }

    [Fact]
    public void SensitiveChange_RejectsValuesToPreventAccidentalPersistence()
    {
        var action = () => new AuditChange(
            Guid.NewGuid(), "DocumentNumber", "old", null, AuditValueProtection.SensitiveIdentifier);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Recorder_CreatesAnIndependentAppendOnlySnapshotDiff()
    {
        var recorder = new AuditRecorder(new AuditDiffService());

        var entry = recorder.RecordSnapshotChange(
            "Journey",
            "JRN-12",
            AuditAction.Updated,
            AuditActor.ForSimulatedUser("user-1", "Operator"),
            new DateTimeOffset(2026, 7, 29, 14, 0, 0, TimeSpan.Zero),
            new Dictionary<string, object?> { ["Notes"] = "before" },
            new Dictionary<string, object?> { ["Notes"] = "after" });

        entry.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            FieldName = "Notes",
            PreviousValue = "before",
            CurrentValue = "after"
        });
    }
}
