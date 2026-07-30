using FluentAssertions;
using Nagomi.Api.Features.ReferenceData;

namespace Nagomi.UnitTests.ReferenceData;

public sealed class IneImporterTests
{
    [Fact]
    public async Task Import_is_idempotent_and_preserves_geographic_relationships()
    {
        var db = new TestNagomiDb();
        var importer = new IneImporter(db);
        var row = new IneImportRow(" 01 ", " Andalucía ", "04", " Almería ", "04013", " Almería ");

        var first = await importer.ImportAsync(Rows(row));
        var second = await importer.ImportAsync(Rows(row));

        first.Should().Be(new ImportResult(3, 0, 0));
        second.Should().Be(new ImportResult(0, 0, 3));
        db.Communities.Should().ContainSingle().Which.Code.Should().Be("01");
        db.Provinces.Should().ContainSingle().Which.AutonomousCommunityCode.Should().Be("01");
        db.Municipalities.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Code = "04013",
            ProvinceCode = "04",
            AutonomousCommunityCode = "01"
        });
    }

    [Fact]
    public async Task Reimport_updates_existing_levels_without_adding_duplicates()
    {
        var db = new TestNagomiDb();
        var importer = new IneImporter(db);
        await importer.ImportAsync(Rows(
            new IneImportRow("01", "Andalucía", "04", "Almería", "04013", "Almería")));

        var result = await importer.ImportAsync(Rows(
            new IneImportRow("01", "Andalucía", "04", "Almería", "04013", "Almería capital", false)));

        result.Should().Be(new ImportResult(0, 3, 0));
        db.Municipalities.Should().ContainSingle().Which.Name.Should().Be("Almería capital");
        db.Municipalities.Single().IsActive.Should().BeFalse();
    }

    private static async IAsyncEnumerable<IneImportRow> Rows(params IneImportRow[] rows)
    {
        foreach (var row in rows)
            yield return row;
        await Task.CompletedTask;
    }
}
