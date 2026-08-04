using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.ReferenceData;

public static class ReferenceDataEndpoints
{
    public static IServiceCollection AddReferenceData(this IServiceCollection services)
    {
        services.AddScoped<IIneImporter, IneImporter>();
        services.AddScoped<ICnhImporter, Cnh2025Importer>();
        services.AddScoped<ITransportReasonLookup, TransportReasonLookup>();
        services.AddSingleton<ICnhRowReader, Cnh2025CsvRowReader>();
        return services;
    }

    public static IEndpointRouteBuilder MapReferenceDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reference-data").RequireAuthorization(UserAuthorizationPolicies.Web).WithTags("Reference data");

        group.MapGet("/autonomous-communities", GetAutonomousCommunities);
        group.MapGet("/provinces", GetProvinces);
        group.MapGet("/municipalities", GetMunicipalities);
        group.MapGet("/transport-reasons", GetTransportReasons);
        group.MapPost("/transport-reasons", CreateTransportReason);
        group.MapPut("/transport-reasons/{id:guid}", UpdateTransportReason);
        group.MapGet("/healthcare-facilities", SearchHealthcareFacilities);
        group.MapGet("/healthcare-facilities/resolve", ResolveHealthcareFacility);
        group.MapPost("/healthcare-facilities", CreateManualHealthcareFacility);
        ReferenceDataImportEndpoints.Map(group);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<IneLookupResponse>>> GetAutonomousCommunities(
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var values = await db.IneAutonomousCommunities.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new IneLookupResponse(x.Code, x.Name))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<IneLookupResponse>>(values);
    }

    private static async Task<Ok<IReadOnlyList<IneLookupResponse>>> GetProvinces(
        string? autonomousCommunityCode,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var query = db.IneProvinces.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(autonomousCommunityCode))
            query = query.Where(x => x.AutonomousCommunityCode == autonomousCommunityCode.Trim());
        var values = await query.OrderBy(x => x.Name)
            .Select(x => new IneLookupResponse(x.Code, x.Name, x.AutonomousCommunityCode))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<IneLookupResponse>>(values);
    }

    private static async Task<Ok<IReadOnlyList<IneLookupResponse>>> GetMunicipalities(
        string? provinceCode,
        string? query,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var municipalities = db.IneMunicipalities.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(provinceCode))
            municipalities = municipalities.Where(x => x.ProvinceCode == provinceCode.Trim());
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            municipalities = municipalities.Where(x => x.Name.ToLower().Contains(term));
        }
        var values = await municipalities.OrderBy(x => x.Name).Take(100)
            .Select(x => new IneLookupResponse(x.Code, x.Name, x.ProvinceCode))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<IneLookupResponse>>(values);
    }

    private static async Task<Ok<IReadOnlyList<TransportReasonResponse>>> GetTransportReasons(
        bool? includeInactive,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var query = db.TransportReasons.AsNoTracking();
        if (includeInactive is not true)
            query = query.Where(x => x.IsActive);
        var values = await query.OrderBy(x => x.Description)
            .Select(x => new TransportReasonResponse(x.Id, x.Code, x.Description, x.IsActive))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<TransportReasonResponse>>(values);
    }

    private static async Task<Results<Created<TransportReasonResponse>, ValidationProblem>> CreateTransportReason(
        UpsertTransportReasonRequest request,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var errors = ValidateReason(request);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.TransportReasons.AnyAsync(x => x.Code == code, cancellationToken))
            return TypedResults.ValidationProblem(Error("code", "A transport reason with this code already exists."));

        var reason = new TransportReason
        {
            Code = code,
            Description = request.Description.Trim(),
            IsActive = request.IsActive
        };
        db.TransportReasons.Add(reason);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/reference-data/transport-reasons/{reason.Id}", reason.ToResponse());
    }

    private static async Task<Results<Ok<TransportReasonResponse>, NotFound, ValidationProblem>> UpdateTransportReason(
        Guid id,
        UpsertTransportReasonRequest request,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var errors = ValidateReason(request);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);
        var reason = await db.TransportReasons.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reason is null)
            return TypedResults.NotFound();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.TransportReasons.AnyAsync(x => x.Id != id && x.Code == code, cancellationToken))
            return TypedResults.ValidationProblem(Error("code", "A transport reason with this code already exists."));

        reason.Code = code;
        reason.Description = request.Description.Trim();
        reason.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(reason.ToResponse());
    }

    private static async Task<Ok<IReadOnlyList<HealthcareFacilityResponse>>> SearchHealthcareFacilities(
        string? query,
        string? municipalityCode,
        bool? includeInactive,
        int? limit,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var facilities = db.HealthcareFacilities.AsNoTracking();
        if (includeInactive is not true)
            facilities = facilities.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(municipalityCode))
            facilities = facilities.Where(x => x.MunicipalityCode == municipalityCode.Trim());
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            facilities = facilities.Where(x => x.Name.ToLower().Contains(term) ||
                (x.Ccn != null && x.Ccn.ToLower().Contains(term)) ||
                (x.Codcnh != null && x.Codcnh.ToLower().Contains(term)));
        }
        var entities = await facilities.OrderBy(x => x.Name).Take(Math.Clamp(limit ?? 25, 1, 100))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<HealthcareFacilityResponse>>(entities.Select(x => x.ToResponse()).ToList());
    }

    private static async Task<Results<Ok<HealthcareFacilityResponse>, NotFound, ValidationProblem>> ResolveHealthcareFacility(
        string? ccn,
        string? codcnh,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        ccn = NullIfWhiteSpace(ccn);
        codcnh = NullIfWhiteSpace(codcnh);
        if (ccn is null && codcnh is null)
            return TypedResults.ValidationProblem(Error("code", "Supply CCN or CODCNH."));

        var facility = await db.HealthcareFacilities.AsNoTracking().SingleOrDefaultAsync(
            x => x.Source == HealthcareFacilitySource.Official &&
                 (codcnh == null || x.Codcnh == codcnh) &&
                 (ccn == null || x.Ccn == ccn),
            cancellationToken);
        return facility is null ? TypedResults.NotFound() : TypedResults.Ok(facility.ToResponse());
    }

    private static async Task<Results<Created<HealthcareFacilityResponse>, ValidationProblem>> CreateManualHealthcareFacility(
        CreateManualHealthcareFacilityRequest request,
        INagomiDb db,
        CancellationToken cancellationToken)
    {
        var errors = ValidateFacility(request);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var facility = new HealthcareFacility
        {
            Name = request.Name.Trim(),
            Source = HealthcareFacilitySource.Manual,
            Street = NullIfWhiteSpace(request.Street),
            Number = NullIfWhiteSpace(request.Number),
            Block = NullIfWhiteSpace(request.Block),
            Staircase = NullIfWhiteSpace(request.Staircase),
            Floor = NullIfWhiteSpace(request.Floor),
            Door = NullIfWhiteSpace(request.Door),
            AdditionalDetails = NullIfWhiteSpace(request.AdditionalDetails),
            PostalCode = NullIfWhiteSpace(request.PostalCode),
            MunicipalityCode = NullIfWhiteSpace(request.MunicipalityCode),
            ProvinceCode = NullIfWhiteSpace(request.ProvinceCode),
            AutonomousCommunityCode = NullIfWhiteSpace(request.AutonomousCommunityCode),
            Phone = NullIfWhiteSpace(request.Phone),
            Notes = NullIfWhiteSpace(request.Notes),
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };
        db.HealthcareFacilities.Add(facility);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/reference-data/healthcare-facilities/{facility.PublicId}", facility.ToResponse());
    }

    private static Dictionary<string, string[]> ValidateReason(UpsertTransportReasonRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 50)
            errors["code"] = ["Code is required and must not exceed 50 characters."];
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 250)
            errors["description"] = ["Description is required and must not exceed 250 characters."];
        return errors;
    }

    private static Dictionary<string, string[]> ValidateFacility(CreateManualHealthcareFacilityRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 250)
            errors["name"] = ["Name is required and must not exceed 250 characters."];
        if (request.Latitude is < -90 or > 90)
            errors["latitude"] = ["Latitude must be between -90 and 90."];
        if (request.Longitude is < -180 or > 180)
            errors["longitude"] = ["Longitude must be between -180 and 180."];
        return errors;
    }

    private static Dictionary<string, string[]> Error(string key, string message) => new() { [key] = [message] };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
