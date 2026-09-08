using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.Dispatch;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Infrastructure.Authentication;
using Nagomi.Api.Infrastructure.PublicIds;

namespace Nagomi.Api.Features.Vehicles;

/// <summary>One row of the coordination board: a journey with its vehicle, driver, status and status points.</summary>
public sealed record CoordinationRow(
    Guid JourneyId,
    string JourneyPublicId,
    Guid RequestId,
    string RequestPublicId,
    JourneyDirection Direction,
    DateTimeOffset OperationalAt,
    string PatientName,
    string Origin,
    string Destination,
    JourneyStatus Status,
    Guid? VehicleId,
    string? VehiclePublicId,
    string? VehicleName,
    string? DriverName,
    IReadOnlyList<StatusPoint> StatusPoints);

/// <summary>A status event with its reported position (world 1 / testimonial map).</summary>
public sealed record StatusPoint(
    Guid Id,
    JourneyStatus Status,
    DateTimeOffset OccurredAt,
    string? Actor,
    string? ExternalResourceCode,
    decimal? Latitude,
    decimal? Longitude);

public sealed record AssignVehicleCommand(Guid? VehicleId);

public sealed record AssignDriverCommand(string? DriverName);

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/vehicles")
            .RequireAuthorization(UserAuthorizationPolicies.Admin)
            .WithTags("Vehicles");
        admin.MapGet("/", ListAsync);
        admin.MapPost("/", CreateAsync);
        admin.MapPut("/{id:guid}", UpdateAsync);
        admin.MapDelete("/{id:guid}", DeleteAsync);

        var web = endpoints.MapGroup("/api")
            .RequireAuthorization(UserAuthorizationPolicies.Web)
            .WithTags("Coordination");
        web.MapGet("/vehicles", ListAsync);
        web.MapGet("/coordination", CoordinationAsync);
        web.MapPut("/journeys/{id:guid}/vehicle", AssignJourneyVehicleAsync);
        web.MapPut("/journeys/{id:guid}/driver", AssignJourneyDriverAsync);

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<VehicleResponse>>, NotFound>> ListAsync(
        ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        if (providerId is null) return TypedResults.NotFound();
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new VehicleResponse(x.Id, x.PublicId, x.Name, x.ExternalCode, x.VehicleType, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<VehicleResponse>>(vehicles);
    }

    private static async Task<Results<Created<VehicleResponse>, NotFound, ValidationProblem>> CreateAsync(
        UpsertVehicleCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        IPublicIdGenerator ids, TimeProvider clock, CancellationToken cancellationToken)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        if (providerId is null) return TypedResults.NotFound();
        if (string.IsNullOrWhiteSpace(command.Name))
            return ValidationProblem("Vehicle name is required.");

        var code = VehicleMapping.Clean(command.Code);
        if (!string.IsNullOrWhiteSpace(code) && await db.Vehicles.AnyAsync(
                x => x.PublicId == code, cancellationToken))
            return ValidationProblem($"Ya existe un vehículo con el código interno '{code}'.");

        var vehicle = new TransportVehicle
        {
            ProviderId = providerId.Value,
            PublicId = string.IsNullOrWhiteSpace(code)
                ? await ids.NextAsync("VHC", cancellationToken)
                : code,
            Name = command.Name.Trim(),
            VehicleType = command.VehicleType,
            ExternalCode = VehicleMapping.Clean(command.ExternalCode),
            IsActive = command.IsActive,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow()
        };
        db.Add(vehicle);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/vehicles/{vehicle.Id}", vehicle.ToResponse());
    }

    private static async Task<Results<Ok<VehicleResponse>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, UpsertVehicleCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        if (providerId is null) return TypedResults.NotFound();
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(
            x => x.Id == id && x.ProviderId == providerId.Value, cancellationToken);
        if (vehicle is null) return TypedResults.NotFound();
        if (string.IsNullOrWhiteSpace(command.Name))
            return ValidationProblem("Vehicle name is required.");

        var code = VehicleMapping.Clean(command.Code);
        if (!string.IsNullOrWhiteSpace(code) && await db.Vehicles.AnyAsync(
                x => x.PublicId == code && x.Id != vehicle.Id, cancellationToken))
            return ValidationProblem($"Ya existe un vehículo con el código interno '{code}'.");

        vehicle.Name = command.Name.Trim();
        vehicle.VehicleType = command.VehicleType;
        if (!string.IsNullOrWhiteSpace(code))
            vehicle.PublicId = code;
        vehicle.ExternalCode = VehicleMapping.Clean(command.ExternalCode);
        vehicle.IsActive = command.IsActive;
        vehicle.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(vehicle.ToResponse());
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        if (providerId is null) return TypedResults.NotFound();
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(
            x => x.Id == id && x.ProviderId == providerId.Value, cancellationToken);
        if (vehicle is null) return TypedResults.NotFound();
        // Soft-delete: keep the row so historical journeys keep their vehicle reference.
        vehicle.IsActive = false;
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<CoordinationRow>>> CoordinationAsync(
        ITransportDb db, IProviderIntegrationDb integrationDb, TimeProvider clock, CancellationToken cancellationToken)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive)
            .Select(x => new { x.Id, x.PublicId, x.Name })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var requests = db.TransportRequests.AsNoTracking();
        var journeys = db.Journeys.AsNoTracking().Include(x => x.StatusHistory)
            .Where(x => x.ServiceDate >= today.AddDays(-1) && x.ServiceDate <= today.AddDays(1));
        var joined = journeys.Join(requests, j => j.TransportRequestId, r => r.Id, (j, r) => new { j, r })
            .Where(x => x.j.CurrentStatus != JourneyStatus.Completed && x.j.CurrentStatus != JourneyStatus.Cancelled);
        if (providerId.HasValue)
            joined = joined.Where(x => x.r.ProviderId == providerId || x.r.ProviderId == null);

        var values = await joined.ToListAsync(cancellationToken);
        var rows = values.Select(x => new CoordinationRow(
            x.j.Id, x.j.PublicId, x.r.Id, x.r.PublicId!,
            x.j.Direction,
            x.j.Direction == JourneyDirection.Return ? x.j.Schedule.ScheduledPickupAt!.Value : x.j.Schedule.ScheduledStartAt,
            x.r.Patient == null ? "" : ((x.r.Patient.FirstName ?? "") + " " + (x.r.Patient.LastName ?? "")).Trim(),
            x.j.Origin.Name ?? x.j.Origin.Street ?? "", x.j.Destination.Name ?? x.j.Destination.Street ?? "",
            x.j.CurrentStatus,
            x.j.VehicleId,
            x.j.VehicleId.HasValue && vehicles.TryGetValue(x.j.VehicleId!.Value, out var v) ? v.PublicId : null,
            x.j.VehicleId.HasValue && vehicles.TryGetValue(x.j.VehicleId!.Value, out var v2) ? v2.Name : null,
            x.j.DriverName,
            x.j.StatusHistory
                .OrderByDescending(s => s.OccurredAt).ThenByDescending(s => s.RecordedAt)
                .Select(s => new StatusPoint(s.Id, s.Status, s.OccurredAt, s.Actor, s.ExternalResourceCode, s.Latitude, s.Longitude))
                .ToArray()))
            .OrderBy(x => x.OperationalAt)
            .ToArray();
        return TypedResults.Ok<IReadOnlyList<CoordinationRow>>(rows);
    }

    private static async Task<Results<Ok<VehicleResponse?>, NotFound, ValidationProblem>> AssignJourneyVehicleAsync(
        Guid id, AssignVehicleCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        IHubContext<DispatchHub> dispatch, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.Include(x => x.Vehicle)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();

        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        VehicleResponse? response = null;
        if (command.VehicleId.HasValue)
        {
            var vehicle = await db.Vehicles.SingleOrDefaultAsync(
                x => x.Id == command.VehicleId.Value && x.ProviderId == providerId && x.IsActive, cancellationToken);
            if (vehicle is null) return ValidationProblem("Vehicle not found or not active for this organization.");
            journey.VehicleId = vehicle.Id;
            journey.Vehicle = vehicle;
            response = vehicle.ToResponse();
        }
        else
        {
            journey.VehicleId = null;
            journey.Vehicle = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Real-time dispatch: tell the assigned vehicle's group there is new work.
        if (journey.VehicleId.HasValue && response is not null)
        {
            var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == journey.VehicleId.Value, cancellationToken);
            if (vehicle is not null && !string.IsNullOrWhiteSpace(vehicle.PublicId))
            {
                await dispatch.Clients.Group(DispatchHub.GroupName(vehicle.PublicId))
                    .SendAsync("WorkAssigned", new DispatchNotification(
                        "journey", journey.PublicId, journey.Id, vehicle.PublicId, DateTimeOffset.UtcNow), cancellationToken);
            }
        }

        return response is null
            ? TypedResults.Ok<VehicleResponse?>(null)
            : TypedResults.Ok<VehicleResponse?>(response);
    }

    /// <summary>Assigns (or clears) the driver name on a journey. A plain name — no worker directory.</summary>
    private static async Task<Results<Ok<string?>, NotFound>> AssignJourneyDriverAsync(
        Guid id, AssignDriverCommand command, ITransportDb db, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();

        journey.DriverName = string.IsNullOrWhiteSpace(command.DriverName)
            ? null
            : command.DriverName.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(journey.DriverName);
    }

    /// <summary>Resolves the tenant's own provider (the self-execution auto-provider) used to scope web vehicle management.</summary>
    private static async Task<Guid?> TenantProviderIdAsync(
        IProviderIntegrationDb db, CancellationToken cancellationToken)
    {
        var code = await db.TransportProviders.AsNoTracking()
            .Where(x => x.Code == "SELF")
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return code;
    }

    private static ValidationProblem ValidationProblem(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["vehicle"] = [message] });
}
