using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.Journeys;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.TransportRequests;

public static class TransportRequestEndpoints
{
    public static IEndpointRouteBuilder MapTransportRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/transport-requests").RequireAuthorization(UserAuthorizationPolicies.Web).WithTags("Transport requests");
        group.MapPost("/drafts", CreateDraft);
        group.MapGet("/{id:guid}", Get);
        group.MapPut("/{id:guid}/draft", UpdateDraft);
        group.MapDelete("/{id:guid}/draft", DeleteDraft);
        group.MapPost("/{id:guid}/submit/one-off", SubmitOneOff);
        group.MapPost("/{id:guid}/submit/recurring", SubmitRecurring);
        group.MapPut("/{id:guid}/snapshot", UpdateSnapshot);
        group.MapPost("/{id:guid}/cancel", Cancel);
        group.MapPost("/{id:guid}/recurrence/preview", PreviewRecurrence);
        group.MapPost("/{id:guid}/recurrence/apply", ApplyRecurrence);
        return endpoints;
    }

    private static async Task<Created<TransportRequestRecord>> CreateDraft(
        TransportRequestSnapshot snapshot, ITransportDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var record = new TransportRequestRecord { CreatedAt = now, UpdatedAt = now };
        record.Apply(snapshot, provider: false);
        db.Add(record);
        Audit(db, record.Id.ToString(), "Created", ChangeSource.Nagomi, "simulated-user", now);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/transport-requests/{record.Id}", record);
    }

    private static async Task<Results<Ok<TransportRequestRecord>, NotFound>> Get(
        Guid id, ITransportDb db, IProviderIntegrationDb integrationDb, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.PublicId is not null)
        {
            request.Deliveries = await integrationDb.ProviderNotifications
                .Where(x => x.EntityPublicId == request.PublicId)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new ProviderDeliveryRecord(
                    x.Id, x.State.ToString(), x.CreatedAt, x.RetrievedAt, x.FailedPublishAttempts))
                .ToListAsync(cancellationToken);
        }
        return TypedResults.Ok(request);
    }

    private static async Task<Results<Ok<TransportRequestRecord>, NotFound, Conflict<string>>> UpdateDraft(
        Guid id, TransportRequestSnapshot snapshot, ITransportDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.Status != TransportRequestStatus.Draft) return TypedResults.Conflict("Only drafts can use the draft update endpoint.");
        request.Apply(snapshot, provider: false);
        request.UpdatedAt = clock.GetUtcNow();
        Audit(db, id.ToString(), "Updated", ChangeSource.Nagomi, "simulated-user", request.UpdatedAt);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(request);
    }

    private static async Task<Results<NoContent, NotFound, Conflict<string>>> DeleteDraft(
        Guid id, ITransportDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.Status != TransportRequestStatus.Draft) return TypedResults.Conflict("Submitted requests cannot be physically deleted.");
        db.Remove(request);
        Audit(db, id.ToString(), "Deleted", ChangeSource.Nagomi, "simulated-user", clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SubmitOneOff(
        Guid id, SubmitOneOffCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.Status != TransportRequestStatus.Draft) return TypedResults.Conflict("The request has already been submitted.");
        try
        {
            var domain = request.ToDomain();
            domain.SubmitOneOff(command.Outbound, command.Return);
            Activate(request, domain, clock.GetUtcNow());
            foreach (var journey in request.JourneyRecords)
                db.Add(journey);
            Audit(db, request.PublicId!, "Submitted", ChangeSource.Nagomi, "simulated-user", request.UpdatedAt);
            await NotifyRequest(request, outbox, "TransportRequestCreated", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(request);
        }
        catch (DomainValidationException exception) { return Validation(exception); }
    }

    private static async Task<IResult> SubmitRecurring(
        Guid id, SubmitRecurringCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.Status != TransportRequestStatus.Draft) return TypedResults.Conflict("The request has already been submitted.");
        try
        {
            var domain = request.ToDomain();
            var pattern = command.Recurrence.ToDomain();
            domain.SubmitRecurring(pattern);
            request.Recurrence = pattern;
            Activate(request, domain, clock.GetUtcNow());
            foreach (var journey in request.JourneyRecords)
                db.Add(journey);
            Audit(db, request.PublicId!, "Submitted", ChangeSource.Nagomi, "simulated-user", request.UpdatedAt);
            await NotifyRequest(request, outbox, "TransportRequestCreated", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(request);
        }
        catch (DomainValidationException exception) { return Validation(exception); }
    }

    private static async Task<IResult> UpdateSnapshot(
        Guid id, UpdateRequestCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.Status == TransportRequestStatus.Draft) return TypedResults.Conflict("Use the draft endpoint before submission.");
        var provider = command.Source == ChangeSource.TransportProvider;
        request.Apply(command.Snapshot, provider);
        request.UpdatedAt = clock.GetUtcNow();
        if (command.PropagateToJourneys && !provider)
        {
            foreach (var journey in request.JourneyRecords.Where(x => !x.Terminal() && (command.OverwriteExceptions || !x.IsRecurrenceException)))
            {
                journey.Origin = request.DefaultOrigin!.Copy();
                journey.Destination = request.DefaultDestination!.Copy();
                if (journey.Direction == JourneyDirection.Return)
                    (journey.Origin, journey.Destination) = (journey.Destination, journey.Origin);
                journey.Requirements = request.Requirements.Copy();
                journey.ProviderVisibleNotes = request.ProviderVisibleNotes;
            }
        }
        Audit(db, request.PublicId!, "Updated", command.Source, command.Actor, request.UpdatedAt);
        if (!provider)
            await NotifyRequest(request, outbox, "TransportRequestUpdated", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(request);
    }

    private static async Task<IResult> Cancel(
        Guid id, CancelCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.Status == TransportRequestStatus.Draft) return TypedResults.Conflict("A draft can be deleted instead of cancelled.");
        var now = clock.GetUtcNow();
        request.Status = TransportRequestStatus.Cancelled;
        foreach (var journey in request.JourneyRecords.Where(x => x.CurrentStatus != JourneyStatus.Completed))
            JourneyCancellation.Apply(journey, command, now, $"request-cancel:{id}:{journey.Id}", db);
        request.UpdatedAt = now;
        Audit(db, request.PublicId!, "Cancelled", command.Source, command.Actor, now);
        if (command.Source != ChangeSource.TransportProvider)
            await NotifyRequest(request, outbox, "TransportRequestCancelled", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(request);
    }

    private static async Task<IResult> PreviewRecurrence(
        Guid id, RecurrenceChangeCommand command, ITransportDb db, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        try { return TypedResults.Ok(BuildImpact(request, command.Recurrence.ToDomain())); }
        catch (DomainValidationException exception) { return Validation(exception); }
    }

    private static async Task<IResult> ApplyRecurrence(
        Guid id, RecurrenceChangeCommand command, ITransportDb db, IProviderOutbox outbox,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var request = await Requests(db).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return TypedResults.NotFound();
        if (request.Status != TransportRequestStatus.Active) return TypedResults.Conflict("Recurrence can only change on active requests.");
        try
        {
            var generated = Generate(request, command.Recurrence.ToDomain());
            var generatedByKey = generated.ToDictionary(x => x.Key());
            var now = clock.GetUtcNow();
            foreach (var existing in request.JourneyRecords.Where(x => !x.Terminal()).ToArray())
            {
                if (!generatedByKey.TryGetValue(existing.Key(), out var replacement))
                {
                    JourneyCancellation.Apply(existing, new CancelCommand(CancellationReason.SchedulingConflict, CancellingParty.Requester), now, $"recurrence:{id}:{existing.Key()}", db);
                    continue;
                }
                if (!existing.IsRecurrenceException || command.OverwriteExceptions)
                    Replace(existing, replacement);
                generatedByKey.Remove(existing.Key());
            }
            foreach (var journey in generatedByKey.Values)
            {
                request.JourneyRecords.Add(journey);
                db.Add(journey);
            }
            request.Recurrence = command.Recurrence.ToDomain();
            request.UpdatedAt = now;
            Audit(db, request.PublicId!, "Updated", ChangeSource.Nagomi, "simulated-user", now);
            await NotifyRequest(request, outbox, "TransportRequestUpdated", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(request);
        }
        catch (DomainValidationException exception) { return Validation(exception); }
    }

    private static IQueryable<TransportRequestRecord> Requests(ITransportDb db) =>
        db.TransportRequests.Include(x => x.JourneyRecords).ThenInclude(x => x.StatusHistory);

    private static void Activate(TransportRequestRecord target, TransportRequest source, DateTimeOffset now)
    {
        target.PublicId = source.PublicId;
        target.Status = source.Status;
        target.JourneyRecords.AddRange(source.Journeys.Select(x => x.ToRecord(target.Id)));
        target.UpdatedAt = now;
    }

    private static List<JourneyRecord> Generate(TransportRequestRecord request, RecurrencePattern recurrence)
    {
        var domain = request.ToDomain();
        domain.SubmitRecurring(recurrence);
        return domain.Journeys.Select(x => x.ToRecord(request.Id)).ToList();
    }

    private static RecurrenceImpact BuildImpact(TransportRequestRecord request, RecurrencePattern recurrence)
    {
        var generated = Generate(request, recurrence).ToDictionary(x => x.Key());
        var eligible = request.JourneyRecords.Where(x => !x.Terminal()).ToArray();
        var existing = eligible.ToDictionary(x => x.Key());
        var additions = generated.Keys.Except(existing.Keys).Order().ToArray();
        var cancellations = existing.Where(x => !generated.ContainsKey(x.Key)).Select(x => x.Value.PublicId).Order().ToArray();
        var changed = generated.Keys.Intersect(existing.Keys).Count(key => !SameSchedule(generated[key], existing[key]));
        return new(additions.Length, cancellations.Length, changed, eligible.Count(x => x.IsRecurrenceException),
            request.JourneyRecords.Count(x => x.Terminal()), additions, cancellations);
    }

    private static bool SameSchedule(JourneyRecord x, JourneyRecord y) =>
        x.Schedule.AppointmentAt == y.Schedule.AppointmentAt &&
        x.Schedule.ScheduledStartAt == y.Schedule.ScheduledStartAt &&
        x.Schedule.ScheduledPickupAt == y.Schedule.ScheduledPickupAt &&
        x.Schedule.PickupTimePending == y.Schedule.PickupTimePending;

    private static void Replace(JourneyRecord target, JourneyRecord source)
    {
        target.Origin = source.Origin;
        target.Destination = source.Destination;
        target.Requirements = source.Requirements;
        target.Schedule = source.Schedule;
        target.ProviderVisibleNotes = source.ProviderVisibleNotes;
        target.IsRecurrenceException = false;
    }

    private static void Audit(ITransportDb db, string id, string action, ChangeSource source, string actor, DateTimeOffset at) =>
        db.Add(new TransportAuditRecord { EntityType = "TransportRequest", EntityIdentifier = id, Action = action, Source = source, Actor = actor, RecordedAt = at });

    private static IResult Validation(DomainValidationException exception) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [exception.Message] });

    private static Task<ProviderNotification?> NotifyRequest(
        TransportRequestRecord request, IProviderOutbox outbox, string messageType, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(request.ContractCode) || string.IsNullOrWhiteSpace(request.PublicId)
            ? Task.FromResult<ProviderNotification?>(null)
            : outbox.AddAsync(request.ContractCode, messageType, IntegrationEntityType.TransportRequest,
                request.PublicId, $"/api/provider/requests/{request.PublicId}", Guid.NewGuid(), cancellationToken);
}

internal static class JourneyCancellation
{
    internal static JourneyStatusRecord Apply(
        JourneyRecord journey, CancelCommand command, DateTimeOffset recordedAt, string fallbackKey, ITransportDb db)
    {
        var key = TransportMapping.Clean(command.IdempotencyKey) ?? fallbackKey;
        var prior = journey.StatusHistory.SingleOrDefault(x => x.IdempotencyKey == key);
        if (prior is not null) return prior;
        var status = new JourneyStatusRecord
        {
            JourneyId = journey.Id, Status = JourneyStatus.Cancelled,
            OccurredAt = command.OccurredAt ?? recordedAt, RecordedAt = recordedAt,
            Source = command.Source, Actor = command.Actor, IdempotencyKey = key,
            CancellationReason = command.Reason, CancellingParty = command.CancellingParty
        };
        journey.StatusHistory.Add(status);
        db.Add(status);
        JourneyMaterializer.Apply(journey);
        return status;
    }
}
