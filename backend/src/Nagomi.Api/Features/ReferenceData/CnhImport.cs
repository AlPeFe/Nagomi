using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ReferenceData;

public sealed record CnhImportRow(
    string Ccn,
    string Codcnh,
    string Name,
    string OfficialAddressText,
    string? Street,
    string? Number,
    string? PostalCode,
    string? MunicipalityCode,
    string? ProvinceCode,
    string? AutonomousCommunityCode,
    string? Phone,
    decimal? Latitude,
    decimal? Longitude,
    bool IsActive);

public interface ICnhRowReader
{
    IAsyncEnumerable<CnhImportRow> ReadAsync(Stream csv, CancellationToken cancellationToken = default);
}

public interface ICnhImporter
{
    Task<ImportResult> ImportAsync(
        IAsyncEnumerable<CnhImportRow> rows,
        CancellationToken cancellationToken = default);
}

public sealed class Cnh2025Importer(INagomiDb db) : ICnhImporter
{
    public async Task<ImportResult> ImportAsync(
        IAsyncEnumerable<CnhImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var officialFacilities = await db.HealthcareFacilities
            .Where(x => x.Source == HealthcareFacilitySource.Official)
            .ToListAsync(cancellationToken);
        var byCcn = officialFacilities.Where(x => x.Ccn is not null)
            .ToDictionary(x => x.Ccn!, StringComparer.OrdinalIgnoreCase);
        var byCodcnh = officialFacilities.Where(x => x.Codcnh is not null)
            .ToDictionary(x => x.Codcnh!, StringComparer.OrdinalIgnoreCase);

        await foreach (var source in rows.WithCancellation(cancellationToken))
        {
            var row = source.Normalize();
            if (row.Codcnh.Length == 0 || row.Ccn.Length == 0 || row.Name.Length == 0)
                throw new InvalidDataException("CNH rows require CCN, CODCNH, and name.");
            if (!seenCodes.Add(row.Codcnh))
                throw new InvalidDataException($"Duplicate CODCNH '{row.Codcnh}' in import stream.");

            byCcn.TryGetValue(row.Ccn, out var ccnMatch);
            byCodcnh.TryGetValue(row.Codcnh, out var codcnhMatch);
            if (ccnMatch is not null && codcnhMatch is not null && ccnMatch.Id != codcnhMatch.Id)
                throw new InvalidDataException(
                    $"CCN '{row.Ccn}' and CODCNH '{row.Codcnh}' resolve different official facilities.");
            var facility = ccnMatch ?? codcnhMatch;

            if (facility is null)
            {
                facility = row.ToFacility();
                db.HealthcareFacilities.Add(facility);
                byCcn.Add(row.Ccn, facility);
                byCodcnh.Add(row.Codcnh, facility);
                added++;
                continue;
            }

            if (facility.Ccn is not null &&
                !string.Equals(facility.Ccn, row.Ccn, StringComparison.OrdinalIgnoreCase))
                byCcn.Remove(facility.Ccn);
            if (facility.Codcnh is not null &&
                !string.Equals(facility.Codcnh, row.Codcnh, StringComparison.OrdinalIgnoreCase))
                byCodcnh.Remove(facility.Codcnh);
            if (facility.Apply(row))
                updated++;
            else
                unchanged++;
            byCcn[row.Ccn] = facility;
            byCodcnh[row.Codcnh] = facility;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportResult(added, updated, unchanged);
    }
}

// Reads bounded UTF-8 CSV directly. It does not execute formulas, macros, links, or archive content.
// Convert the official workbook to CSV in a trusted deployment step before passing it here.
public sealed class Cnh2025CsvRowReader : ICnhRowReader
{
    private const int MaxRows = 100_000;
    private const int MaxFieldLength = 16_384;

    public async IAsyncEnumerable<CnhImportRow> ReadAsync(
        Stream csv,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            csv,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        var records = ReadRecordsAsync(reader, cancellationToken);
        await using var enumerator = records.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync())
            yield break;

        var headers = enumerator.Current
            .Select((value, index) => (Name: NormalizeHeader(value), Index: index))
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        var rowNumber = 1;

        while (await enumerator.MoveNextAsync())
        {
            if (++rowNumber > MaxRows)
                throw new InvalidDataException($"CSV exceeds the {MaxRows} row safety limit.");

            var values = enumerator.Current;
            if (values.All(string.IsNullOrWhiteSpace))
                continue;

            yield return new CnhImportRow(
                Required(values, headers, "CCN"),
                Required(values, headers, "CODCNH"),
                Required(values, headers, "NOMBRE", "NAME"),
                Optional(values, headers, "DIRECCION", "OFFICIALADDRESSTEXT") ?? string.Empty,
                Optional(values, headers, "VIA", "STREET"),
                Optional(values, headers, "NUMERO", "NUMBER"),
                Optional(values, headers, "CODIGOPOSTAL", "POSTALCODE", "CP"),
                Optional(values, headers, "CODIGOMUNICIPIO", "MUNICIPALITYCODE"),
                Optional(values, headers, "CODIGOPROVINCIA", "PROVINCECODE"),
                Optional(values, headers, "CODIGOCA", "AUTONOMOUSCOMMUNITYCODE"),
                Optional(values, headers, "TELEFONO", "PHONE"),
                Decimal(values, headers, "LATITUD", "LATITUDE"),
                Decimal(values, headers, "LONGITUD", "LONGITUDE"),
                Boolean(values, headers, defaultValue: true, "ACTIVO", "ISACTIVE"));
        }
    }

    private static async IAsyncEnumerable<string[]> ReadRecordsAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length > MaxFieldLength * 32)
                throw new InvalidDataException("CSV record exceeds the safety limit.");
            yield return ParseRecord(line);
        }
    }

    private static string[] ParseRecord(string line)
    {
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    index++;
                    field.Append('"');
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(ch);
                if (field.Length > MaxFieldLength)
                    throw new InvalidDataException($"CSV field exceeds the {MaxFieldLength} character safety limit.");
            }
        }

        if (quoted)
            throw new InvalidDataException("Multiline or unterminated quoted CSV fields are not accepted.");
        record.Add(field.ToString());
        return record.ToArray();
    }

    private static string Required(string[] values, Dictionary<string, int> headers, params string[] names) =>
        Optional(values, headers, names) ?? throw new InvalidDataException($"Required CSV column {names[0]} is missing or empty.");

    private static string? Optional(string[] values, Dictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValue(NormalizeHeader(name), out var index) && index < values.Length)
            {
                var value = values[index].Trim();
                return value.Length == 0 ? null : value;
            }
        }
        return null;
    }

    private static decimal? Decimal(string[] values, Dictionary<string, int> headers, params string[] names)
    {
        var value = Optional(values, headers, names);
        if (value is null)
            return null;
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ||
            decimal.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("es-ES"), out result))
            return result;
        throw new InvalidDataException($"'{value}' is not a valid decimal in column {names[0]}.");
    }

    private static bool Boolean(
        string[] values,
        Dictionary<string, int> headers,
        bool defaultValue,
        params string[] names)
    {
        var value = Optional(values, headers, names);
        if (value is null)
            return defaultValue;
        return value.ToUpperInvariant() switch
        {
            "1" or "S" or "SI" or "TRUE" or "Y" or "YES" => true,
            "0" or "N" or "NO" or "FALSE" => false,
            _ => throw new InvalidDataException($"'{value}' is not a valid boolean in column {names[0]}.")
        };
    }

    private static string NormalizeHeader(string value) => new(
        value.Trim().Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark &&
                         char.IsLetterOrDigit(ch))
            .Select(char.ToUpperInvariant)
            .ToArray());
}

internal static class CnhImportMapping
{
    public static CnhImportRow Normalize(this CnhImportRow row) => row with
    {
        Ccn = row.Ccn.Trim(),
        Codcnh = row.Codcnh.Trim(),
        Name = row.Name.Trim(),
        OfficialAddressText = row.OfficialAddressText.Trim()
    };

    public static HealthcareFacility ToFacility(this CnhImportRow row) => new()
    {
        Name = row.Name,
        Source = HealthcareFacilitySource.Official,
        Ccn = row.Ccn,
        Codcnh = row.Codcnh,
        OfficialAddressText = row.OfficialAddressText,
        Street = row.Street,
        Number = row.Number,
        PostalCode = row.PostalCode,
        MunicipalityCode = row.MunicipalityCode,
        ProvinceCode = row.ProvinceCode,
        AutonomousCommunityCode = row.AutonomousCommunityCode,
        Phone = row.Phone,
        Latitude = row.Latitude,
        Longitude = row.Longitude,
        SourceYear = 2025,
        IsActive = row.IsActive
    };

    public static bool Apply(this HealthcareFacility facility, CnhImportRow row)
    {
        var changed = facility.Ccn != row.Ccn || facility.Codcnh != row.Codcnh || facility.Name != row.Name ||
                      facility.OfficialAddressText != row.OfficialAddressText || facility.Street != row.Street ||
                      facility.Number != row.Number || facility.PostalCode != row.PostalCode ||
                      facility.MunicipalityCode != row.MunicipalityCode || facility.ProvinceCode != row.ProvinceCode ||
                      facility.AutonomousCommunityCode != row.AutonomousCommunityCode || facility.Phone != row.Phone ||
                      facility.Latitude != row.Latitude || facility.Longitude != row.Longitude ||
                      facility.SourceYear != 2025 || facility.IsActive != row.IsActive;
        if (!changed)
            return false;

        facility.Ccn = row.Ccn;
        facility.Codcnh = row.Codcnh;
        facility.Name = row.Name;
        facility.OfficialAddressText = row.OfficialAddressText;
        facility.Street = row.Street;
        facility.Number = row.Number;
        facility.PostalCode = row.PostalCode;
        facility.MunicipalityCode = row.MunicipalityCode;
        facility.ProvinceCode = row.ProvinceCode;
        facility.AutonomousCommunityCode = row.AutonomousCommunityCode;
        facility.Phone = row.Phone;
        facility.Latitude = row.Latitude;
        facility.Longitude = row.Longitude;
        facility.SourceYear = 2025;
        facility.IsActive = row.IsActive;
        return true;
    }
}
