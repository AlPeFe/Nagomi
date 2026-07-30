using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Nagomi.Api.Features.ReferenceData;
using Nagomi.IntegrationTests.Api;

namespace Nagomi.IntegrationTests.ReferenceData;

public sealed class ReferenceDataImportEndpointTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Ine_json_array_import_is_idempotent()
    {
        var rows = new[]
        {
            new IneImportRow("91", "Import Community", "91", "Import Province", "91001", "Import Town")
        };

        var first = await _client.PostAsJsonAsync("/api/reference-data/imports/ine", rows);
        var second = await _client.PostAsJsonAsync("/api/reference-data/imports/ine", rows);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ImportResult>()).Should().Be(new ImportResult(3, 0, 0));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<ImportResult>()).Should().Be(new ImportResult(0, 0, 3));
    }

    [Fact]
    public async Task Ine_ndjson_rows_are_imported()
    {
        const string row = """
            {"autonomousCommunityCode":"92","autonomousCommunityName":"NDJSON Community","provinceCode":"92","provinceName":"NDJSON Province","municipalityCode":"92001","municipalityName":"NDJSON Town"}
            """;
        using var content = new StringContent(row + "\n", Encoding.UTF8, "application/x-ndjson");

        var response = await _client.PostAsync("/api/reference-data/imports/ine", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ImportResult>()).Should().Be(new ImportResult(3, 0, 0));
    }

    [Fact]
    public async Task Cnh_raw_csv_import_is_idempotent()
    {
        const string csv = "CCN,CODCNH,NOMBRE,DIRECCION,CODIGOMUNICIPIO,ACTIVO\n" +
                           "CCN-IMPORT,CNH-IMPORT,Hospital Import,Calle Uno 1,91001,SI\n";

        var first = await PostCsv(csv);
        var second = await PostCsv(csv);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ImportResult>()).Should().Be(new ImportResult(1, 0, 0));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<ImportResult>()).Should().Be(new ImportResult(0, 0, 1));
    }

    [Fact]
    public async Task Import_endpoints_reject_xlsx_and_oversized_bodies()
    {
        using var xlsx = new ByteArrayContent([0x50, 0x4b, 0x03, 0x04]);
        xlsx.Headers.ContentType = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var xlsxResponse = await _client.PostAsync("/api/reference-data/imports/cnh", xlsx);

        using var oversized = new ByteArrayContent(new byte[16 * 1024 * 1024 + 1]);
        oversized.Headers.ContentType = new("text/csv");
        var oversizedResponse = await _client.PostAsync("/api/reference-data/imports/cnh", oversized);

        xlsxResponse.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        oversizedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private Task<HttpResponseMessage> PostCsv(string csv) =>
        _client.PostAsync(
            "/api/reference-data/imports/cnh",
            new StringContent(csv, Encoding.UTF8, "text/csv"));
}
