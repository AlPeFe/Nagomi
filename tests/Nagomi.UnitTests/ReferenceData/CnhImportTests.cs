using System.Text;
using FluentAssertions;
using Nagomi.Api.Features.ReferenceData;

namespace Nagomi.UnitTests.ReferenceData;

public sealed class CnhImportTests
{
    [Fact]
    public async Task Csv_reader_parses_official_fields_quotes_spanish_numbers_and_active_state()
    {
        const string csv = "CCN,CODCNH,NOMBRE,DIRECCIÓN,VÍA,NÚMERO,CP,CODIGOMUNICIPIO,CODIGOPROVINCIA,CODIGOCA,TELÉFONO,LATITUD,LONGITUD,ACTIVO\n" +
                           "CCN-1,CNH-1,\"Hospital, General\",\"Calle Mayor, 2\",Mayor,2,28001,28079,28,13,91555,\"40,4168\",\"-3,7038\",N\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = await new Cnh2025CsvRowReader().ReadAsync(stream).ToListAsync();

        rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new CnhImportRow(
            "CCN-1", "CNH-1", "Hospital, General", "Calle Mayor, 2", "Mayor", "2", "28001",
            "28079", "28", "13", "91555", 40.4168m, -3.7038m, false));
    }

    [Fact]
    public async Task Import_is_idempotent_and_retains_2025_catalog_data()
    {
        var db = new TestNagomiDb();
        var importer = new Cnh2025Importer(db);
        var row = Row();

        var first = await importer.ImportAsync(Rows(row));
        var second = await importer.ImportAsync(Rows(row));

        first.Should().Be(new ImportResult(1, 0, 0));
        second.Should().Be(new ImportResult(0, 0, 1));
        db.Facilities.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Source = HealthcareFacilitySource.Official,
            Ccn = "CCN-1",
            Codcnh = "CNH-1",
            OfficialAddressText = "Calle Mayor, 2",
            MunicipalityCode = "28079",
            ProvinceCode = "28",
            AutonomousCommunityCode = "13",
            SourceYear = 2025,
            IsActive = true
        });
    }

    [Fact]
    public async Task Import_rejects_duplicate_official_code_in_one_catalog()
    {
        var importer = new Cnh2025Importer(new TestNagomiDb());

        var action = () => importer.ImportAsync(Rows(Row(), Row() with { Ccn = "CCN-2" }));

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*Duplicate CODCNH*");
    }

    [Fact]
    public async Task Reimport_updates_changed_codcnh_when_ccn_identifies_existing_facility()
    {
        var db = new TestNagomiDb();
        var importer = new Cnh2025Importer(db);
        await importer.ImportAsync(Rows(Row()));

        var result = await importer.ImportAsync(Rows(Row() with { Codcnh = "CNH-2" }));

        result.Should().Be(new ImportResult(0, 1, 0));
        db.Facilities.Should().ContainSingle().Which.Codcnh.Should().Be("CNH-2");
    }

    private static CnhImportRow Row() => new(
        " CCN-1 ", " CNH-1 ", " Hospital General ", " Calle Mayor, 2 ", "Mayor", "2", "28001",
        "28079", "28", "13", "91555", 40.4168m, -3.7038m, true);

    private static async IAsyncEnumerable<CnhImportRow> Rows(params CnhImportRow[] rows)
    {
        foreach (var row in rows)
            yield return row;
        await Task.CompletedTask;
    }
}
