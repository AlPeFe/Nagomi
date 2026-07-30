using FluentAssertions;
using Nagomi.Api.Features.ReferenceData;

namespace Nagomi.UnitTests.ReferenceData;

public sealed class TransportReasonTests
{
    [Fact]
    public async Task Deactivation_blocks_new_snapshot_lookup_but_does_not_change_historical_snapshot()
    {
        var reason = new TransportReason { Code = "CONS", Description = "Consultation" };
        var db = new TestNagomiDb();
        db.Reasons.Seed(reason);
        var lookup = new TransportReasonLookup(db);

        var historical = await lookup.FindActiveSnapshotAsync(reason.Id);
        reason.Description = "Changed description";
        reason.IsActive = false;
        var newSelection = await lookup.FindActiveSnapshotAsync(reason.Id);

        historical.Should().Be(new TransportReasonSnapshot("CONS", "Consultation"));
        newSelection.Should().BeNull();
    }
}
