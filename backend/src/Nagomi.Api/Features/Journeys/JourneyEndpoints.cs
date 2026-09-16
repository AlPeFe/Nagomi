using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.Tenant;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.Journeys;

public sealed record JourneySnapshotCommand(
    LocationSnapshot Origin,
    LocationSnapshot Destination,
    TransportRequirements Requirements,
    JourneySchedule Schedule,
    string? ProviderVisibleNotes,
    string? ProviderReference,
    ChangeSource Source = ChangeSource.Nagomi,
    string Actor = "simulated-user");

public sealed record AddJourneyStatusCommand(
    JourneyStatus Status,
    DateTimeOffset OccurredAt,
    string IdempotencyKey,
    ChangeSource Source,
    string Actor,
    string? ExternalResourceCode = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    CancellationReason? CancellationReason = null,
    CancellingParty? CancellingParty = null);

public sealed record AssignVehicleCommand(Guid VehicleId, string? Actor = null);

public sealed record AdjudicateVehicleCommand(Guid? VehicleId = null, string? Actor = null);

public sealed record UnadjudicateVehicleCommand(string? Actor = null);

public sealed record ResetJourneyCommand(
    ChangeSource Source = ChangeSource.Nagomi,
    string Actor = "simulated-user");

public static class JourneyEndpoints
{
    public static IEndpointRouteBuilder MapJourneyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/journeys").RequireAuthorization(UserAuthorizationPolicies.Web).WithTags("Journeys");
        group.MapGet("/{id:guid}", Get);
        group.MapPut("/{id:guid}/snapshot", UpdateSnapshot);
        group.MapPost("/{id:guid}/cancel", Cancel);
        group.MapPost("/{id:guid}/reset", Reset);
        group.MapPost("/{id:guid}/statuses", AddStatus);
        group.MapPost("/{id:guid}/assign-vehicle", AssignVehicle);
        group.MapPost("/{id:guid}/adjudicate-vehicle", AdjudicateVehicle);
        group.MapPost("/{id:guid}/unadjudicate-vehicle", UnadjudicateVehicle);
        group.MapGet("/{id:guid}/statuses", GetStatusHistory);
        return endpoints;
    }

    private static async Task<Results<Ok<JourneyRecord>, NotFound>> Get(
        Guid id, ITransportDb db, CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return journey is null ? TypedResults.NotFound() : TypedResults.Ok(journey);
    }

    private static async Task<IResult> UpdateSnapshot(
        Guid id, JourneySnapshotCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();
        if (journey.Terminal()) return TypedResults.Conflict("Completed and cancelled journeys cannot be edited.");
        try
        {
            ValidateLocations(command.Origin, command.Destination);
            journey.Origin = command.Origin.Copy();
            journey.Destination = command.Destination.Copy();
            journey.Requirements = command.Requirements.Copy();
            journey.Schedule = command.Schedule.Copy();
            journey.ProviderVisibleNotes = TransportMapping.Clean(command.ProviderVisibleNotes);
            journey.ProviderReference = TransportMapping.Clean(command.ProviderReference);
            journey.IsRecurrenceException = true;
            journey.ExternallyModified = command.Source == ChangeSource.TransportProvider;
            Audit(db, journey, "Updated", command.Source, command.Actor, clock.GetUtcNow());
            if (command.Source != ChangeSource.TransportProvider)
                await NotifyJourney(journey, db, outbox, "JourneyUpdated", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(journey);
        }
        catch (DomainValidationException exception) { return Validation(exception); }
    }

    private static async Task<IResult> Cancel(
        Guid id, CancelCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();
        if (journey.CurrentStatus == JourneyStatus.Completed) return TypedResults.Conflict("A completed journey cannot be cancelled.");
        // Cancelar exige motivo y parte que cancela: sin ellos se guardaba un 0 por
        // defecto (NoLongerRequired/solicitante) y la cancelación no era trazable.
        if (command.Reason is null || command.CancellingParty is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cancellationReason"] = ["Cancelar un traslado exige indicar el motivo."],
                ["cancellingParty"] = ["Cancelar un traslado exige indicar quién cancela."]
            });
        JourneyCancellation.Apply(journey, command, clock.GetUtcNow(), $"journey-cancel:{id}", db);
        Audit(db, journey, "Cancelled", command.Source, command.Actor, clock.GetUtcNow());
        if (command.Source != ChangeSource.TransportProvider)
            await NotifyJourney(journey, db, outbox, "JourneyCancelled", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(journey);
    }

    private static async Task<IResult> Reset(
        Guid id, ResetJourneyCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();
        if (journey.CurrentStatus == JourneyStatus.Completed)
            return TypedResults.Conflict("A completed journey cannot be reset.");
        if (journey.StatusHistory.Count == 0)
            return TypedResults.Ok(journey);

        // Roll the journey back to a clean Scheduled state: drop the status history,
        // clear the derived actuals, and keep the assigned vehicle/driver/route data.
        foreach (var status in journey.StatusHistory.ToArray())
            db.Remove(status);
        journey.StatusHistory.Clear();
        journey.CurrentStatus = JourneyStatus.Scheduled;
        journey.ActualActivatedAt = null;
        journey.ActualArrivedAtOriginAt = null;
        journey.ActualPatientPickupAt = null;
        journey.ActualArrivedAtDestinationAt = null;
        journey.ActualCompletedAt = null;
        journey.CurrentCancellationReason = null;
        journey.CurrentCancellingParty = null;
        journey.ExternallyModified = false;
        Audit(db, journey, "Reset", command.Source, command.Actor, clock.GetUtcNow());
        if (command.Source != ChangeSource.TransportProvider)
            await NotifyJourney(journey, db, outbox, "JourneyUpdated", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(journey);
    }

    private static async Task<IResult> AddStatus(
        Guid id, AddJourneyStatusCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();
        try
        {
            var prior = journey.StatusHistory.SingleOrDefault(x => x.IdempotencyKey == command.IdempotencyKey.Trim());
            if (prior is not null) return TypedResults.Ok(prior);
            ValidateStatus(journey, command);
            var status = new JourneyStatusRecord
            {
                JourneyId = journey.Id, Status = command.Status, OccurredAt = command.OccurredAt,
                RecordedAt = clock.GetUtcNow(), Source = command.Source, Actor = command.Actor.Trim(),
                IdempotencyKey = command.IdempotencyKey.Trim(), ExternalResourceCode = TransportMapping.Clean(command.ExternalResourceCode),
                Latitude = command.Latitude, Longitude = command.Longitude,
                CancellationReason = command.CancellationReason, CancellingParty = command.CancellingParty
            };
            journey.StatusHistory.Add(status);
            db.Add(status);
            JourneyMaterializer.Apply(journey);
            if (command.Source != ChangeSource.TransportProvider)
                await NotifyJourney(journey, db, outbox, "JourneyStatusChanged", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(status);
        }
        catch (DomainValidationException exception) { return Validation(exception); }
    }

    /// <summary>
    /// ASIGNAR es un placeholder: se propone un vehículo sin comprometerlo. No publica nada,
    /// no habilita el retrieve y no genera solicitud en Rabbit.
    /// </summary>
    private static async Task<IResult> AssignVehicle(
        Guid id, AssignVehicleCommand command, ITransportDb db, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();
        if (journey.Terminal()) return TypedResults.Conflict("Un traslado completado o anulado no admite cambios de vehículo.");
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.VehicleId, cancellationToken);
        if (vehicle is null) return Validation("vehicleId", "El vehículo indicado no existe.");
        if (journey.AdjudicatedAt is not null && journey.VehicleId != vehicle.Id)
            return TypedResults.Conflict("El traslado está adjudicado: desadjudícalo antes de cambiar de vehículo.");
        journey.VehicleId = vehicle.Id;
        Audit(db, journey, "VehicleAssigned", ChangeSource.Nagomi, ActorName(command.Actor), clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(journey);
    }

    /// <summary>
    /// ADJUDICAR compromete el vehículo con este traslado: a partir de aquí el proveedor recibe
    /// la solicitud en Rabbit y puede hacer retrieve. Es el único punto que publica un traslado.
    /// </summary>
    private static async Task<IResult> AdjudicateVehicle(
        Guid id, AdjudicateVehicleCommand command, ITransportDb db, IProviderOutbox outbox,
        ITenantDb tenantDb, TimeProvider clock, CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();
        if (journey.Terminal()) return TypedResults.Conflict("Un traslado completado o anulado no se puede adjudicar.");
        var vehicleId = command.VehicleId ?? journey.VehicleId;
        if (vehicleId is null) return Validation("vehicleId", "Indica el vehículo que realizará el traslado.");
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == vehicleId.Value, cancellationToken);
        if (vehicle is null) return Validation("vehicleId", "El vehículo indicado no existe.");

        journey.VehicleId = vehicle.Id;
        journey.AdjudicatedAt = clock.GetUtcNow();
        journey.AdjudicatedBy = ActorName(command.Actor);
        Audit(db, journey, "VehicleAdjudicated", ChangeSource.Nagomi, journey.AdjudicatedBy, clock.GetUtcNow());
        // La solicitud al proveedor nace AQUÍ (no al crear el traslado), y sólo si hay destino:
        // en modo publicador el destino lo define la cola del CLIENTE; si el cliente no tiene cola
        // no se publica en ninguna cola y el traslado queda expuesto únicamente por la API.
        var clientQueue = await ClientQueueAsync(journey, db, tenantDb, cancellationToken);
        if (PublicationPolicy.ShouldPublish(await ExecutesOwnFleetAsync(tenantDb, cancellationToken), clientQueue))
            await NotifyJourney(journey, db, outbox, "JourneyAssigned", cancellationToken, targetQueue: clientQueue);
        else
            Audit(db, journey, "VehicleAdjudicatedNotPublished", ChangeSource.Nagomi, journey.AdjudicatedBy, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(journey);
    }

    /// <summary>
    /// DESADJUDICAR libera el vehículo para poder cambiarlo: retira del outbox lo que aún no ha
    /// salido y avisa al proveedor de lo que ya había recibido. El vehículo asignado se conserva
    /// como propuesta.
    /// </summary>
    private static async Task<IResult> UnadjudicateVehicle(
        Guid id, UnadjudicateVehicleCommand command, ITransportDb db, IProviderIntegrationDb integrationDb,
        IProviderOutbox outbox, TimeProvider clock, CancellationToken cancellationToken)
    {
        var journey = await Query(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (journey is null) return TypedResults.NotFound();
        if (journey.AdjudicatedAt is null)
            return TypedResults.Conflict("El traslado no está adjudicado.");

        var notifications = await integrationDb.ProviderNotifications
            .Where(x => x.EntityType == IntegrationEntityType.Journey && x.EntityPublicId == journey.PublicId
                && x.State != NotificationDeliveryState.Dead)
            .ToListAsync(cancellationToken);
        var alreadyDelivered = notifications.Any(x => x.State is NotificationDeliveryState.Published or NotificationDeliveryState.Retrieved);
        var pending = notifications.Where(x => x.State == NotificationDeliveryState.Pending).ToList();
        if (pending.Count > 0)
            integrationDb.ProviderNotifications.RemoveRange(pending);

        journey.AdjudicatedAt = null;
        journey.AdjudicatedBy = null;
        Audit(db, journey, "VehicleUnadjudicated", ChangeSource.Nagomi, ActorName(command.Actor), clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        // Lo ya publicado se retira explícitamente (el traslado sigue vivo, pero sin vehículo).
        if (alreadyDelivered)
        {
            await NotifyJourney(journey, db, outbox, "JourneyUnassigned", cancellationToken, force: true);
            await db.SaveChangesAsync(cancellationToken);
        }
        return TypedResults.Ok(journey);
    }

    private static string ActorName(string? value) => string.IsNullOrWhiteSpace(value) ? "web-user" : value.Trim();

    private static IResult Validation(string key, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static async Task<Results<Ok<IReadOnlyList<JourneyStatusRecord>>, NotFound>> GetStatusHistory(
        Guid id, ITransportDb db, CancellationToken cancellationToken)
    {
        if (!await db.Journeys.AnyAsync(x => x.Id == id, cancellationToken)) return TypedResults.NotFound();
        var history = await db.Journeys.Where(x => x.Id == id).SelectMany(x => x.StatusHistory)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.RecordedAt)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<JourneyStatusRecord>>(history);
    }

    private static IQueryable<JourneyRecord> Query(ITransportDb db) => db.Journeys.Include(x => x.StatusHistory);

    private static void ValidateLocations(LocationSnapshot origin, LocationSnapshot destination)
    {
        if (origin.Type == LocationType.PrivateAddress && destination.Type == LocationType.PrivateAddress)
            throw new DomainValidationException("A private-to-private journey is not allowed.");
    }

    private static void ValidateStatus(JourneyRecord journey, AddJourneyStatusCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new DomainValidationException("An idempotency key is required.");
        if (string.IsNullOrWhiteSpace(command.Actor)) throw new DomainValidationException("A status actor is required.");
        if (command.Latitude is < -90 or > 90 || command.Longitude is < -180 or > 180)
            throw new DomainValidationException("Coordinates are outside their valid ranges.");
        if (command.Status == JourneyStatus.Cancelled && (command.CancellationReason is null || command.CancellingParty is null))
            throw new DomainValidationException("Cancellation reason and cancelling party are required.");
        if (command.Status != JourneyStatus.Cancelled && (command.CancellationReason.HasValue || command.CancellingParty.HasValue))
            throw new DomainValidationException("Cancellation metadata is only valid for a cancelled status.");
        if (journey.CurrentStatus == JourneyStatus.Completed && command.OccurredAt >= journey.ActualCompletedAt)
            throw new DomainValidationException("A completed journey cannot be reopened.");
    }

    private static void Audit(ITransportDb db, JourneyRecord journey, string action, ChangeSource source, string actor, DateTimeOffset at) =>
        db.Add(new TransportAuditRecord { EntityType = "Journey", EntityIdentifier = journey.PublicId, Action = action, Source = source, Actor = actor, RecordedAt = at });

    private static IResult Validation(DomainValidationException exception) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["journey"] = [exception.Message] });

    /// <summary>Cola del cliente del traslado, si la define (opcional por diseño).</summary>
    private static async Task<string?> ClientQueueAsync(
        JourneyRecord journey, ITransportDb db, ITenantDb tenantDb, CancellationToken cancellationToken)
    {
        var clientId = await db.TransportRequests.AsNoTracking()
            .Where(x => x.Id == journey.TransportRequestId)
            .Select(x => x.ClientId)
            .SingleOrDefaultAsync(cancellationToken);
        if (clientId is null)
            return null;
        var queue = await tenantDb.TransportClients.AsNoTracking()
            .Where(x => x.Id == clientId.Value)
            .Select(x => x.RabbitQueue)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(queue) ? null : queue.Trim();
    }

    /// <summary>
    /// ¿La instalación ejecuta su propia flota? Si no hay fila de configuración (instalación recién
    /// creada o entorno de pruebas) se asume que SÍ: es el default del modelo y, sobre todo, evita
    /// callar publicaciones por un dato que aún no existe. Sólo se deja de publicar cuando consta
    /// que la instalación no ejecuta flota Y el cliente no tiene cola.
    /// </summary>
    private static async Task<bool> ExecutesOwnFleetAsync(ITenantDb tenantDb, CancellationToken cancellationToken)
    {
        var settings = await tenantDb.TenantSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantSettings.SingletonId, cancellationToken);
        return settings is null || settings.Capabilities.HasFlag(TenantCapabilities.ExecutesTransports);
    }

    private static async Task NotifyJourney(
        JourneyRecord journey, ITransportDb db, IProviderOutbox outbox, string messageType,
        CancellationToken cancellationToken, bool force = false, string? targetQueue = null)
    {
        // Sólo se publica lo ADJUDICADO: mientras el vehículo sea una propuesta, el proveedor
        // no recibe nada en Rabbit ni puede recuperar el traslado. `force` se usa para la
        // retirada explícita al desadjudicar (cuando ya no está adjudicado).
        if (!force && journey.AdjudicatedAt is null)
            return;
        var request = await db.TransportRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == journey.TransportRequestId, cancellationToken);
        if (request?.ContractCode is null)
            return;
        await outbox.AddAsync(request.ContractCode, messageType, IntegrationEntityType.Journey,
            journey.PublicId, $"/api/provider/journeys/{journey.PublicId}", Guid.NewGuid(), cancellationToken,
            targetQueue);
    }
}

public static class JourneyMaterializer
{
    public static void Apply(JourneyRecord journey)
    {
        var current = journey.StatusHistory.OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).First();
        journey.CurrentStatus = current.Status;
        journey.CurrentCancellationReason = current.Status == JourneyStatus.Cancelled ? current.CancellationReason : null;
        journey.CurrentCancellingParty = current.Status == JourneyStatus.Cancelled ? current.CancellingParty : null;
        journey.ActualActivatedAt = Latest(journey, JourneyStatus.Activated);
        journey.ActualArrivedAtOriginAt = Latest(journey, JourneyStatus.ArrivedAtOrigin);
        journey.ActualPatientPickupAt = Latest(journey, JourneyStatus.PatientOnBoard);
        journey.ActualArrivedAtDestinationAt = Latest(journey, JourneyStatus.ArrivedAtDestination);
        journey.ActualCompletedAt = Latest(journey, JourneyStatus.Completed);
    }

    private static DateTimeOffset? Latest(JourneyRecord journey, JourneyStatus status) =>
        journey.StatusHistory.Where(x => x.Status == status).Select(x => (DateTimeOffset?)x.OccurredAt).Max();
}
