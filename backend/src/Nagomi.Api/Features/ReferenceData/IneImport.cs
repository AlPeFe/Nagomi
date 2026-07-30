using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ReferenceData;

public sealed record IneImportRow(
    string AutonomousCommunityCode,
    string AutonomousCommunityName,
    string ProvinceCode,
    string ProvinceName,
    string MunicipalityCode,
    string MunicipalityName,
    bool IsActive = true);

public sealed record ImportResult(int Added, int Updated, int Unchanged);

public interface IIneRowSource
{
    IAsyncEnumerable<IneImportRow> ReadAsync(Stream stream, CancellationToken cancellationToken = default);
}

public interface IIneImporter
{
    Task<ImportResult> ImportAsync(
        IAsyncEnumerable<IneImportRow> rows,
        CancellationToken cancellationToken = default);
}

public sealed class IneImporter(INagomiDb db) : IIneImporter
{
    public async Task<ImportResult> ImportAsync(
        IAsyncEnumerable<IneImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var communities = await db.IneAutonomousCommunities.ToDictionaryAsync(x => x.Code, cancellationToken);
        var provinces = await db.IneProvinces.ToDictionaryAsync(x => x.Code, cancellationToken);
        var municipalities = await db.IneMunicipalities.ToDictionaryAsync(x => x.Code, cancellationToken);

        await foreach (var source in rows.WithCancellation(cancellationToken))
        {
            var row = source.Normalize();
            if (row.AutonomousCommunityCode.Length == 0 || row.ProvinceCode.Length == 0 ||
                row.MunicipalityCode.Length == 0 || row.AutonomousCommunityName.Length == 0 ||
                row.ProvinceName.Length == 0 || row.MunicipalityName.Length == 0)
            {
                throw new InvalidDataException("INE rows require codes and names at all three geographic levels.");
            }

            if (!communities.TryGetValue(row.AutonomousCommunityCode, out var community))
            {
                community = new IneAutonomousCommunity
                {
                    Code = row.AutonomousCommunityCode,
                    Name = row.AutonomousCommunityName,
                    IsActive = row.IsActive
                };
                db.IneAutonomousCommunities.Add(community);
                communities.Add(community.Code, community);
                added++;
            }
            else if (community.Name != row.AutonomousCommunityName || community.IsActive != row.IsActive)
            {
                community.Name = row.AutonomousCommunityName;
                community.IsActive = row.IsActive;
                updated++;
            }
            else
            {
                unchanged++;
            }

            if (!provinces.TryGetValue(row.ProvinceCode, out var province))
            {
                province = new IneProvince
                {
                    Code = row.ProvinceCode,
                    Name = row.ProvinceName,
                    AutonomousCommunityCode = row.AutonomousCommunityCode,
                    IsActive = row.IsActive
                };
                db.IneProvinces.Add(province);
                provinces.Add(province.Code, province);
                added++;
            }
            else if (province.Name != row.ProvinceName ||
                     province.AutonomousCommunityCode != row.AutonomousCommunityCode ||
                     province.IsActive != row.IsActive)
            {
                province.Name = row.ProvinceName;
                province.AutonomousCommunityCode = row.AutonomousCommunityCode;
                province.IsActive = row.IsActive;
                updated++;
            }
            else
            {
                unchanged++;
            }

            if (!municipalities.TryGetValue(row.MunicipalityCode, out var municipality))
            {
                municipality = new IneMunicipality
                {
                    Code = row.MunicipalityCode,
                    Name = row.MunicipalityName,
                    ProvinceCode = row.ProvinceCode,
                    AutonomousCommunityCode = row.AutonomousCommunityCode,
                    IsActive = row.IsActive
                };
                db.IneMunicipalities.Add(municipality);
                municipalities.Add(municipality.Code, municipality);
                added++;
            }
            else if (municipality.Name != row.MunicipalityName ||
                     municipality.ProvinceCode != row.ProvinceCode ||
                     municipality.AutonomousCommunityCode != row.AutonomousCommunityCode ||
                     municipality.IsActive != row.IsActive)
            {
                municipality.Name = row.MunicipalityName;
                municipality.ProvinceCode = row.ProvinceCode;
                municipality.AutonomousCommunityCode = row.AutonomousCommunityCode;
                municipality.IsActive = row.IsActive;
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportResult(added, updated, unchanged);
    }
}

internal static class IneImportNormalization
{
    public static IneImportRow Normalize(this IneImportRow row) => new(
        row.AutonomousCommunityCode.Trim(),
        row.AutonomousCommunityName.Trim(),
        row.ProvinceCode.Trim(),
        row.ProvinceName.Trim(),
        row.MunicipalityCode.Trim(),
        row.MunicipalityName.Trim(),
        row.IsActive);
}
