using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Infrastructure.Authentication;
using Nagomi.Api.Infrastructure.PublicIds;

namespace Nagomi.Api.Features.Routes;

public sealed record CollectiveRouteStopResponse(Guid JourneyId, string JourneyPublicId, int Order, string PatientName, string Origin, string Destination, string Status);

/// <summary>Lightweight vehicle projection used to decorate route responses.</summary>
public sealed record VehicleInfo(Guid Id, string PublicId, string Name);

public sealed record RouteResponse(
    Guid Id,
    string PublicId,
    DateOnly ServiceDate,
    Guid? VehicleId,
    string? VehiclePublicId,
    string? VehicleName,
    string? DriverName,
    string? Notes,
    RouteStatus Status,
    IReadOnlyList<CollectiveRouteStopResponse> Stops);

/// <summary>Create (or replace) a collective route for a service date from the given journey ids.</summary>
public sealed record UpsertRouteCommand(
    DateOnly ServiceDate,
    Guid[] JourneyIds,
    Guid? VehicleId = null,
    string? DriverName = null,
    string? Notes = null);

public static class RouteEndpoints
{
    public static IEndpointRouteBuilder MapRouteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/routes")
            .RequireAuthorization(UserAuthorizationPolicies.Web)
            .WithTags("Collective routes");

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPut("/{id:guid}/vehicle", AssignVehicleAsync);
        group.MapPut("/{id:guid}/driver", AssignDriverAsync);
        group.MapPost("/{id:guid}/complete", CompleteAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync);

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<RouteResponse>>, NotFound>> ListAsync(
        ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken,
        DateOnly? date = null)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive)
            .Select(x => new VehicleInfo(x.Id, x.PublicId, x.Name))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        IQueryable<CollectiveRoute> query = db.Routes.AsNoTracking().Include(x => x.Stops);
        if (date.HasValue)
            query = query.Where(x => x.ServiceDate == date.Value);
        var routes = await query
            .OrderBy(x => x.ServiceDate).ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var journeyIds = routes.SelectMany(r => r.Stops).Select(s => s.JourneyId).Distinct().ToArray();
        var journeys = await db.Journeys.AsNoTracking()
            .Where(x => journeyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var requests = await db.TransportRequests.AsNoTracking()
            .Where(r => journeys.Values.Select(j => j.TransportRequestId).Contains(r.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return TypedResults.Ok<IReadOnlyList<RouteResponse>>(routes.Select(r => Map(r, vehicles, journeys, requests)).ToArray());
    }

    private static async Task<Results<Ok<RouteResponse>, NotFound>> GetAsync(
        Guid id, ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken)
    {
        var result = await FetchAsync(id, db, integrationDb, cancellationToken);
        if (result is null) return TypedResults.NotFound();
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created<RouteResponse>, NotFound, ValidationProblem>> CreateAsync(
        UpsertRouteCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        IPublicIdGenerator ids, TimeProvider clock, CancellationToken cancellationToken)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        if (providerId is null) return TypedResults.NotFound();

        var journeyIds = (command.JourneyIds ?? []).Distinct().ToArray();
        if (journeyIds.Length == 0)
            return ValidationProblem("Selecciona al menos un trayecto para la ruta.");
        if (journeyIds.Length == 1)
            return ValidationProblem("Una ruta colectiva necesita al menos dos trayectos. Para un traslado individual no hace falta ruta.");

        // Validate journeys exist, belong to the service date and are not terminal.
        var journeys = await db.Journeys.AsNoTracking()
            .Where(x => journeyIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (journeys.Count != journeyIds.Length)
            return ValidationProblem("Alguno de los trayectos seleccionados no existe.");
        if (journeys.Any(x => x.ServiceDate != command.ServiceDate))
            return ValidationProblem("Todos los trayectos deben ser del mismo día de servicio.");
        if (journeys.Any(x => x.CurrentStatus is JourneyStatus.Completed or JourneyStatus.Cancelled))
            return ValidationProblem("No se pueden agrupar trayectos completados o cancelados.");

        var route = new CollectiveRoute
        {
            PublicId = await ids.NextAsync("RUT", cancellationToken),
            ServiceDate = command.ServiceDate,
            VehicleId = command.VehicleId,
            DriverName = Clean(command.DriverName),
            Notes = Clean(command.Notes),
            Status = RouteStatus.Planned,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow(),
            Stops = journeys.Select((j, index) => new CollectiveRouteStop { JourneyId = j.Id, Order = index + 1 }).ToList()
        };
        db.Add(route);
        await db.SaveChangesAsync(cancellationToken);

        var created = await FetchAsync(route.Id, db, integrationDb, cancellationToken);
        return TypedResults.Created($"/api/routes/{route.Id}", created!);
    }

    private static async Task<Results<Ok<RouteResponse>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, UpsertRouteCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var route = await db.Routes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (route is null) return TypedResults.NotFound();
        if (route.Status is RouteStatus.Completed or RouteStatus.Cancelled)
            return ValidationProblem("Una ruta completada o cancelada no se puede modificar.");

        var journeyIds = (command.JourneyIds ?? []).Distinct().ToArray();
        if (journeyIds.Length == 0)
            return ValidationProblem("Selecciona al menos un trayecto para la ruta.");
        if (journeyIds.Length == 1)
            return ValidationProblem("Una ruta colectiva necesita al menos dos trayectos.");

        var journeys = await db.Journeys.AsNoTracking()
            .Where(x => journeyIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (journeys.Count != journeyIds.Length)
            return ValidationProblem("Alguno de los trayectos seleccionados no existe.");
        if (journeys.Any(x => x.ServiceDate != route.ServiceDate))
            return ValidationProblem("Todos los trayectos deben ser del mismo día de servicio.");
        if (journeys.Any(x => x.CurrentStatus is JourneyStatus.Completed or JourneyStatus.Cancelled))
            return ValidationProblem("No se pueden agrupar trayectos completados o cancelados.");

        route.Stops = journeys.Select((j, index) => new CollectiveRouteStop { RouteId = route.Id, JourneyId = j.Id, Order = index + 1 }).ToList();
        route.VehicleId = command.VehicleId;
        route.DriverName = Clean(command.DriverName);
        route.Notes = Clean(command.Notes);
        route.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var updated = await FetchAsync(route.Id, db, integrationDb, cancellationToken);
        return TypedResults.Ok(updated!);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ITransportDb db, CancellationToken cancellationToken)
    {
        var route = await db.Routes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (route is null) return TypedResults.NotFound();
        if (route.Status == RouteStatus.Completed) return TypedResults.NoContent(); // keep history
        db.Remove(route);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<RouteResponse>, NotFound, ValidationProblem>> AssignVehicleAsync(
        Guid id, RouteAssignVehicleCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        CancellationToken cancellationToken)
    {
        var route = await db.Routes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (route is null) return TypedResults.NotFound();
        if (route.Status is RouteStatus.Completed or RouteStatus.Cancelled)
            return ValidationProblem("La ruta ya está cerrada.");

        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        if (command.VehicleId.HasValue)
        {
            var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == command.VehicleId.Value && x.ProviderId == providerId && x.IsActive, cancellationToken);
            if (vehicle is null) return ValidationProblem("Vehículo no encontrado o no activo.");
            route.VehicleId = vehicle.Id;
        }
        else
        {
            route.VehicleId = null;
        }
        route.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var updated = await FetchAsync(route.Id, db, integrationDb, cancellationToken);
        return TypedResults.Ok(updated!);
    }

    private static async Task<Results<Ok<RouteResponse>, NotFound, ValidationProblem>> AssignDriverAsync(
        Guid id, RouteAssignDriverCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        CancellationToken cancellationToken)
    {
        var route = await db.Routes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (route is null) return TypedResults.NotFound();
        if (route.Status is RouteStatus.Completed or RouteStatus.Cancelled)
            return ValidationProblem("La ruta ya está cerrada.");

        route.DriverName = string.IsNullOrWhiteSpace(command.DriverName) ? null : command.DriverName.Trim();
        route.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var updated = await FetchAsync(route.Id, db, integrationDb, cancellationToken);
        return TypedResults.Ok(updated!);
    }

    private static async Task<Results<Ok<RouteResponse>, NotFound, ValidationProblem>> CompleteAsync(
        Guid id, ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken)
    {
        var route = await db.Routes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (route is null) return TypedResults.NotFound();
        if (route.Status is RouteStatus.Cancelled) return ValidationProblem("La ruta está cancelada.");
        route.Status = RouteStatus.Completed;
        route.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var updated = await FetchAsync(route.Id, db, integrationDb, cancellationToken);
        return TypedResults.Ok(updated!);
    }

    private static async Task<Results<Ok<RouteResponse>, NotFound>> CancelAsync(
        Guid id, ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken)
    {
        var route = await db.Routes.Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (route is null) return TypedResults.NotFound();
        if (route.Status == RouteStatus.Completed) return TypedResults.Ok(await FetchAsync(id, db, integrationDb, cancellationToken) ?? throw new InvalidOperationException());
        route.Status = RouteStatus.Cancelled;
        route.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var updated = await FetchAsync(id, db, integrationDb, cancellationToken);
        return TypedResults.Ok(updated!);
    }

    // ---- shared helpers ----

    private static async Task<RouteResponse?> FetchAsync(
        Guid id, ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken)
    {
        var providerId = await TenantProviderIdAsync(integrationDb, cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive)
            .Select(x => new VehicleInfo(x.Id, x.PublicId, x.Name))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var route = await db.Routes.AsNoTracking().Include(x => x.Stops).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (route is null) return null;

        var journeys = await db.Journeys.AsNoTracking()
            .Where(x => route.Stops.Select(s => s.JourneyId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var requests = await db.TransportRequests.AsNoTracking()
            .Where(r => journeys.Values.Select(j => j.TransportRequestId).Contains(r.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return Map(route, vehicles, journeys, requests);
    }

    private static RouteResponse Map(
        CollectiveRoute route,
        Dictionary<Guid, VehicleInfo> vehicles,
        Dictionary<Guid, JourneyRecord> journeys,
        Dictionary<Guid, TransportRequestRecord> requests) =>
        new(
            route.Id, route.PublicId, route.ServiceDate,
            route.VehicleId,
            route.VehicleId.HasValue && vehicles.TryGetValue(route.VehicleId.Value, out var v) ? (string?)v.PublicId : null,
            route.VehicleId.HasValue && vehicles.TryGetValue(route.VehicleId.Value, out var v2) ? (string?)v2.Name : null,
            route.DriverName, route.Notes, route.Status,
            route.Stops.OrderBy(s => s.Order).Select(s =>
            {
                journeys.TryGetValue(s.JourneyId, out var journey);
                var patient = journey is null || !requests.TryGetValue(journey.TransportRequestId, out var req) || req.Patient is null
                    ? ""
                    : ((req.Patient.FirstName ?? "") + " " + (req.Patient.LastName ?? "")).Trim();
                return new CollectiveRouteStopResponse(
                    s.JourneyId,
                    journey?.PublicId ?? "",
                    s.Order,
                    patient,
                    journey?.Origin.Name ?? journey?.Origin.Street ?? "",
                    journey?.Destination.Name ?? journey?.Destination.Street ?? "",
                    journey?.CurrentStatus.ToString() ?? "");
            }).ToArray());

    /// <summary>Resolves the tenant's own provider (self-execution) used to scope route vehicle assignment.</summary>
    private static async Task<Guid?> TenantProviderIdAsync(IProviderIntegrationDb db, CancellationToken cancellationToken) =>
        await db.TransportProviders.AsNoTracking()
            .Where(x => x.Code == "SELF")
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private static ValidationProblem ValidationProblem(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["route"] = [message] });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record RouteAssignVehicleCommand(Guid? VehicleId);
public sealed record RouteAssignDriverCommand(string? DriverName);
