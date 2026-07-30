using FluentAssertions;
using Nagomi.Api.Features.Audit;

namespace Nagomi.UnitTests.Audit;

public sealed class AuditDiffServiceTests
{
    private readonly AuditDiffService _service = new();

    [Fact]
    public void Compare_RecordsEveryChangedFieldFromCompleteSnapshots()
    {
        var previous = Snapshot(
            ("Notes", "old"),
            ("ScheduledAt", new DateTimeOffset(2026, 7, 29, 8, 0, 0, TimeSpan.Zero)),
            ("RemovedField", "value"));
        var current = Snapshot(
            ("Notes", "new"),
            ("ScheduledAt", new DateTimeOffset(2026, 7, 29, 9, 0, 0, TimeSpan.Zero)),
            ("AddedField", true));

        var changes = _service.Compare(previous, current);

        changes.Should().BeEquivalentTo(
        [
            new { FieldName = "AddedField", PreviousValue = (string?)null, CurrentValue = (string?)"True" },
            new { FieldName = "Notes", PreviousValue = (string?)"old", CurrentValue = (string?)"new" },
            new { FieldName = "RemovedField", PreviousValue = (string?)"value", CurrentValue = (string?)null },
            new
            {
                FieldName = "ScheduledAt",
                PreviousValue = (string?)"2026-07-29T08:00:00.0000000+00:00",
                CurrentValue = (string?)"2026-07-29T09:00:00.0000000+00:00"
            }
        ], options => options.ExcludingMissingMembers().WithStrictOrdering());
    }

    [Fact]
    public void Compare_OmitsUnchangedFields()
    {
        var snapshot = Snapshot(("Notes", "same"), ("Companion", true));

        _service.Compare(snapshot, snapshot).Should().BeEmpty();
    }

    [Theory]
    [InlineData("DocumentNumber")]
    [InlineData("Patient.NationalIdentifier")]
    [InlineData("HealthCardNumber")]
    [InlineData("SocialSecurityNumber")]
    public void Compare_DoesNotRetainSensitiveIdentifierValues(string fieldName)
    {
        var changes = _service.Compare(
            Snapshot((fieldName, "sensitive-old")),
            Snapshot((fieldName, "sensitive-new")));

        changes.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            FieldName = fieldName,
            PreviousValue = (string?)null,
            CurrentValue = (string?)null,
            Protection = AuditValueProtection.SensitiveIdentifier
        });
    }

    [Fact]
    public void Compare_MasksPreviousAndCurrentPhoneValues()
    {
        var changes = _service.Compare(
            Snapshot(("ContactPhone", "+34 612 345 678")),
            Snapshot(("ContactPhone", "+34 698 765 432")));

        changes.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            FieldName = "ContactPhone",
            PreviousValue = "+** *** *** 678",
            CurrentValue = "+** *** *** 432",
            Protection = AuditValueProtection.MaskedPhone
        });
    }

    [Fact]
    public void Compare_FullyMasksShortPhoneValues()
    {
        var changes = _service.Compare(
            Snapshot(("Phone", "112")),
            Snapshot(("Phone", "061")));

        changes.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PreviousValue = "***",
            CurrentValue = "***",
            Protection = AuditValueProtection.MaskedPhone
        });
    }

    private static IReadOnlyDictionary<string, object?> Snapshot(
        params (string Field, object? Value)[] values) =>
        values.ToDictionary(value => value.Field, value => value.Value, StringComparer.OrdinalIgnoreCase);
}
