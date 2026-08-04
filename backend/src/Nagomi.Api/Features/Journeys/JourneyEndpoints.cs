using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Features.ProviderIntegration;
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

public static class JourneyEndpoints
{
    public static IEndpointRouteBuilder MapJourneyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/journeys").RequireAuthorization(UserAuthorizationPolicies.Web).WithTags("Journeys");
        group.MapGet("/{id:guid}", Get);
        group.MapPut("/{id:guid}/snapshot", UpdateSnapshot);
        group.MapPost("/{id:guid}/cancel", Cancel);
        group.MapPost("/{id:guid}/statuses", AddStatus);
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
        JourneyCancellation.Apply(journey, command, clock.GetUtcNow(), $"journey-cancel:{id}", db);
        Audit(db, journey, "Cancelled", command.Source, command.Actor, clock.GetUtcNow());
        if (command.Source != ChangeSource.TransportProvider)
            await NotifyJourney(journey, db, outbox, "JourneyCancelled", cancellationToken);
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

    private static async Task NotifyJourney(
        JourneyRecord journey, ITransportDb db, IProviderOutbox outbox, string messageType,
        CancellationToken cancellationToken)
    {
        var request = await db.TransportRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == journey.TransportRequestId, cancellationToken);
        if (request?.ContractCode is null)
            return;
        await outbox.AddAsync(request.ContractCode, messageType, IntegrationEntityType.Journey,
            journey.PublicId, $"/api/provider/journeys/{journey.PublicId}", Guid.NewGuid(), cancellationToken);
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
