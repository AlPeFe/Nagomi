using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nagomi.Api.Features.ReferenceData;

/// <summary>
/// Idempotent startup seed for the Spanish reference data: the INE municipality/province/
/// community hierarchy and the CNH 2025 hospital catalogue. Ships as embedded resources so a
/// fresh deployment gets a working reference data set without manual import.
/// </summary>
public static class ReferenceDataSeeder
{
    private const string IneResource = "Nagomi.Api.Data.ine_municipalities.ndjson";
    private const string CnhResource = "Nagomi.Api.Data.cnh_hospitals.csv";

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<INagomiDb>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ReferenceDataSeeder");
        var assembly = typeof(ReferenceDataSeeder).Assembly;

        if (!await db.IneMunicipalities.AnyAsync())
        {
            await using var stream = Open(IneResource, assembly);
            var importer = provider.GetRequiredService<IIneImporter>();
            var result = await importer.ImportAsync(ReadNdjson(stream, CancellationToken.None), CancellationToken.None);
            logger.LogInformation("Seeded INE reference data: {Added} added, {Updated} updated, {Unchanged} unchanged.",
                result.Added, result.Updated, result.Unchanged);
        }
        else
        {
            logger.LogInformation("INE reference data already present; skipping seed.");
        }

        if (!await db.HealthcareFacilities.AnyAsync(x => x.Source == HealthcareFacilitySource.Official))
        {
            await using var stream = Open(CnhResource, assembly);
            var reader = provider.GetRequiredService<ICnhRowReader>();
            var importer = provider.GetRequiredService<ICnhImporter>();
            var result = await importer.ImportAsync(reader.ReadAsync(stream, CancellationToken.None), CancellationToken.None);
            logger.LogInformation("Seeded CNH hospital catalogue: {Added} added, {Updated} updated, {Unchanged} unchanged.",
                result.Added, result.Updated, result.Unchanged);
        }
        else
        {
            logger.LogInformation("CNH hospital catalogue already present; skipping seed.");
        }
    }

    private static Stream Open(string resource, Assembly assembly) =>
        assembly.GetManifestResourceStream(resource)
        ?? throw new InvalidOperationException($"Missing embedded resource '{resource}'.");

    /// <summary>The embedded NDJSON was generated from the same row shape the admin import endpoint accepts.</summary>
    private static async IAsyncEnumerable<IneImportRow> ReadNdjson(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            yield return System.Text.Json.JsonSerializer.Deserialize<IneImportRow>(
                line, System.Text.Json.JsonSerializerOptions.Web)
                ?? throw new InvalidDataException("INE import rows cannot be null.");
        }
    }
}
