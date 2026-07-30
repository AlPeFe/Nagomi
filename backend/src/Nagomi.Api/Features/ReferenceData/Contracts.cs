namespace Nagomi.Api.Features.ReferenceData;

public sealed record IneLookupResponse(string Code, string Name, string? ParentCode = null);

public sealed record TransportReasonResponse(Guid Id, string Code, string Description, bool IsActive);

public sealed record UpsertTransportReasonRequest(string Code, string Description, bool IsActive = true);

public sealed record HealthcareFacilityResponse(
    Guid PublicId,
    string Name,
    HealthcareFacilitySource Source,
    string? Ccn,
    string? Codcnh,
    string? OfficialAddressText,
    StructuredAddress Address,
    string? Phone,
    string? Notes,
    decimal? Latitude,
    decimal? Longitude,
    int? SourceYear,
    bool IsActive);

public sealed record CreateManualHealthcareFacilityRequest(
    string Name,
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
    string? AutonomousCommunityCode,
    string? Phone,
    string? Notes,
    decimal? Latitude,
    decimal? Longitude);

internal static class ReferenceDataContractMapping
{
    public static TransportReasonResponse ToResponse(this TransportReason reason) =>
        new(reason.Id, reason.Code, reason.Description, reason.IsActive);

    public static HealthcareFacilityResponse ToResponse(this HealthcareFacility facility) =>
        new(
            facility.PublicId,
            facility.Name,
            facility.Source,
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
            facility.Notes,
            facility.Latitude,
            facility.Longitude,
            facility.SourceYear,
            facility.IsActive);
}
