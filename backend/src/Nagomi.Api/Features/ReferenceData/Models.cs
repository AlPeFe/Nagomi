namespace Nagomi.Api.Features.ReferenceData;

public sealed class IneAutonomousCommunity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class IneProvince
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string AutonomousCommunityCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class IneMunicipality
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string ProvinceCode { get; set; }
    public required string AutonomousCommunityCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TransportReason
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Description { get; set; }
    public bool IsActive { get; set; } = true;
}

// Requests persist this value object, rather than a live master-data relationship.
public sealed record TransportReasonSnapshot(string Code, string Description);

public enum HealthcareFacilitySource
{
    Official,
    Manual
}

public sealed class HealthcareFacility
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public HealthcareFacilitySource Source { get; set; }
    public string? Ccn { get; set; }
    public string? Codcnh { get; set; }
    public string? OfficialAddressText { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Block { get; set; }
    public string? Staircase { get; set; }
    public string? Floor { get; set; }
    public string? Door { get; set; }
    public string? AdditionalDetails { get; set; }
    public string? PostalCode { get; set; }
    public string? MunicipalityCode { get; set; }
    public string? ProvinceCode { get; set; }
    public string? AutonomousCommunityCode { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? SourceYear { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record HealthcareFacilitySnapshot(
    Guid PublicId,
    string Name,
    string? Ccn,
    string? Codcnh,
    string? OfficialAddressText,
    StructuredAddress Address,
    string? Phone,
    decimal? Latitude,
    decimal? Longitude);

public sealed record StructuredAddress(
    string? Street,
    string? Number,
    string? Block,
    string? Staircase,
    string? Floor,
    string? Door,
    string? AdditionalDetails,
    string? PostalCode,
    string? MunicipalityCode,
    string? ProvinceCode,
    string? AutonomousCommunityCode);

public static class ReferenceDataSnapshots
{
    public static TransportReasonSnapshot Snapshot(this TransportReason reason) =>
        new(reason.Code, reason.Description);

    public static HealthcareFacilitySnapshot Snapshot(this HealthcareFacility facility) =>
        new(
            facility.PublicId,
            facility.Name,
            facility.Ccn,
            facility.Codcnh,
            facility.OfficialAddressText,
            new StructuredAddress(
                facility.Street,
                facility.Number,
                facility.Block,
                facility.Staircase,
                facility.Floor,
                facility.Door,
                facility.AdditionalDetails,
                facility.PostalCode,
                facility.MunicipalityCode,
                facility.ProvinceCode,
                facility.AutonomousCommunityCode),
            facility.Phone,
            facility.Latitude,
            facility.Longitude);
}
