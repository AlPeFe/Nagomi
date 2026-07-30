using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.Journeys;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.Api.Features.ProviderIntegration;

public sealed class TransportProviderResourceGateway(ITransportDb db) : IProviderResourceGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProviderResourceSnapshot?> GetRequestAsync(
        string publicId, CancellationToken cancellationToken)
    {
        var request = await Requests().AsNoTracking()
            .SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        return request is null ? null : Snapshot(request, request);
    }

    public async Task<ProviderResourceSnapshot?> GetJourneyAsync(
        string publicId, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.AsNoTracking().Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        if (journey is null) return null;

        var request = await db.TransportRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == journey.TransportRequestId, cancellationToken);
        return Snapshot(request, journey);
    }

    public async Task<ProviderResourceAuthorization?> GetRequestAuthorizationAsync(
        string publicId, CancellationToken cancellationToken)
    {
        var request = await db.TransportRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        return Authorization(request);
    }

    public async Task<ProviderResourceAuthorization?> GetJourneyAuthorizationAsync(
        string publicId, CancellationToken cancellationToken)
    {
        var request = await (
            from journey in db.Journeys
            join candidate in db.TransportRequests on journey.TransportRequestId equals candidate.Id
            where journey.PublicId == publicId
            select candidate).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return Authorization(request);
    }

    public async Task<ProviderCommandResult> ExecuteAsync(
        string commandType,
        string entityPublicId,
        JsonElement payload,
        ProviderIdentity provider,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            return commandType switch
            {
                "request.replace" => await ReplaceRequestAsync(entityPublicId, payload, provider, acceptedAt, cancellationToken),
                "journey.replace" => await ReplaceJourneyAsync(entityPublicId, payload, provider, acceptedAt, cancellationToken),
                "request.journey.add" => await AddExceptionalJourneyAsync(entityPublicId, payload, provider, acceptedAt, cancellationToken),
                "request.cancel" => await CancelRequestAsync(entityPublicId, payload, provider, acceptedAt, cancellationToken),
                "journey.cancel" => await CancelJourneyAsync(entityPublicId, payload, provider, acceptedAt, cancellationToken),
                "journey.status" => await AddJourneyStatusAsync(entityPublicId, payload, provider, acceptedAt, cancellationToken),
                _ => Error(400, "Unsupported provider command.")
            };
        }
        catch (JsonException exception)
        {
            return Error(400, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Error(400, exception.Message);
        }
        catch (DomainValidationException exception)
        {
            return Error(400, exception.Message);
        }
    }

    private async Task<ProviderCommandResult> ReplaceRequestAsync(
        string publicId, JsonElement payload, ProviderIdentity provider, DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        var request = await Requests().SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        var denied = AccessFailure(request, provider);
        if (denied is not null) return denied;

        var command = Required<UpdateRequestCommand>(payload);
        var target = request!;
        target.Apply(command.Snapshot, provider: true);
        target.UpdatedAt = acceptedAt;
        Audit("TransportRequest", target.PublicId!, "Updated", provider.ClientId, acceptedAt);
        await db.SaveChangesAsync(cancellationToken);
        return Success(target);
    }

    private async Task<ProviderCommandResult> ReplaceJourneyAsync(
        string publicId, JsonElement payload, ProviderIdentity provider, DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        if (journey is null) return Error(404, "Journey not found.");
        var request = await db.TransportRequests.SingleOrDefaultAsync(
            x => x.Id == journey.TransportRequestId, cancellationToken);
        var denied = AccessFailure(request, provider);
        if (denied is not null) return denied;
        if (journey.Terminal()) return Error(409, "Completed and cancelled journeys cannot be edited.");

        var supplied = Required<JourneySnapshotPayload>(payload);
        var command = supplied.ToCommand(journey.Direction);
        ValidateLocations(command.Origin, command.Destination);
        journey.Origin = command.Origin.Copy();
        journey.Destination = command.Destination.Copy();
        journey.Requirements = command.Requirements.Copy();
        journey.Schedule = command.Schedule.Copy();
        journey.ProviderVisibleNotes = TransportMapping.Clean(command.ProviderVisibleNotes);
        journey.ProviderReference = TransportMapping.Clean(command.ProviderReference);
        journey.IsRecurrenceException = true;
        journey.ExternallyModified = true;
        Audit("Journey", journey.PublicId, "Updated", provider.ClientId, acceptedAt);
        await db.SaveChangesAsync(cancellationToken);
        return Success(journey);
    }

    private async Task<ProviderCommandResult> AddExceptionalJourneyAsync(
        string publicId, JsonElement payload, ProviderIdentity provider, DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        var request = await Requests().SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        var denied = AccessFailure(request, provider);
        if (denied is not null) return denied;
        if (request!.Status != TransportRequestStatus.Active)
            return Error(409, "Exceptional journeys can only be added to active requests.");

        var supplied = Required<ExceptionalJourneyPayload>(payload);
        var command = supplied.ToCommand();
        if (request.JourneyRecords.Any(x => x.ServiceDate == command.ServiceDate &&
                x.Direction == command.Direction && !x.Terminal()))
            return Error(409, "An active journey already exists for that date and direction.");
        ValidateLocations(command.Origin, command.Destination);

        var journey = new JourneyRecord
        {
            TransportRequestId = request.Id,
            PublicId = $"JRN-{Guid.NewGuid():N}".ToUpperInvariant(),
            Direction = command.Direction,
            ServiceDate = command.ServiceDate,
            Origin = command.Origin.Copy(),
            Destination = command.Destination.Copy(),
            Requirements = command.Requirements.Copy(),
            Schedule = command.Schedule.Copy(),
            ProviderVisibleNotes = TransportMapping.Clean(command.ProviderVisibleNotes),
            ProviderReference = TransportMapping.Clean(command.ProviderReference),
            IsManuallyAdded = true,
            IsRecurrenceException = true,
            ExternallyModified = true
        };
        request.JourneyRecords.Add(journey);
        request.UpdatedAt = acceptedAt;
        Audit("Journey", journey.PublicId, "Created", provider.ClientId, acceptedAt);
        await db.SaveChangesAsync(cancellationToken);
        return Success(journey, 201);
    }

    private async Task<ProviderCommandResult> CancelRequestAsync(
        string publicId, JsonElement payload, ProviderIdentity provider, DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        var request = await Requests().SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        var denied = AccessFailure(request, provider);
        if (denied is not null) return denied;
        if (request!.Status == TransportRequestStatus.Draft)
            return Error(409, "A draft can be deleted instead of cancelled.");

        var supplied = Required<CancelCommand>(payload);
        var command = supplied with { Source = ChangeSource.TransportProvider, Actor = provider.ClientId };
        request.Status = TransportRequestStatus.Cancelled;
        foreach (var journey in request.JourneyRecords.Where(x => x.CurrentStatus != JourneyStatus.Completed))
        {
            JourneyCancellation.Apply(journey, command, acceptedAt, $"provider-request-cancel:{request.Id}:{journey.Id}", db);
            journey.ExternallyModified = true;
        }
        request.UpdatedAt = acceptedAt;
        Audit("TransportRequest", request.PublicId!, "Cancelled", provider.ClientId, acceptedAt);
        await db.SaveChangesAsync(cancellationToken);
        return Success(request);
    }

    private async Task<ProviderCommandResult> CancelJourneyAsync(
        string publicId, JsonElement payload, ProviderIdentity provider, DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        if (journey is null) return Error(404, "Journey not found.");
        var request = await db.TransportRequests.SingleOrDefaultAsync(
            x => x.Id == journey.TransportRequestId, cancellationToken);
        var denied = AccessFailure(request, provider);
        if (denied is not null) return denied;
        if (journey.CurrentStatus == JourneyStatus.Completed)
            return Error(409, "A completed journey cannot be cancelled.");

        var supplied = Required<CancelCommand>(payload);
        var command = supplied with { Source = ChangeSource.TransportProvider, Actor = provider.ClientId };
        JourneyCancellation.Apply(journey, command, acceptedAt, $"provider-journey-cancel:{journey.Id}", db);
        journey.ExternallyModified = true;
        Audit("Journey", journey.PublicId, "Cancelled", provider.ClientId, acceptedAt);
        await db.SaveChangesAsync(cancellationToken);
        return Success(journey);
    }

    private async Task<ProviderCommandResult> AddJourneyStatusAsync(
        string publicId, JsonElement payload, ProviderIdentity provider, DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
        if (journey is null) return Error(404, "Journey not found.");
        var request = await db.TransportRequests.SingleOrDefaultAsync(
            x => x.Id == journey.TransportRequestId, cancellationToken);
        var denied = AccessFailure(request, provider);
        if (denied is not null) return denied;

        var supplied = Required<AddJourneyStatusCommand>(payload);
        var command = supplied with { Source = ChangeSource.TransportProvider, Actor = provider.ClientId };
        var key = command.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key)) throw new DomainValidationException("An idempotency key is required.");
        var prior = journey.StatusHistory.SingleOrDefault(x => x.IdempotencyKey == key);
        if (prior is not null) return Success(prior);
        ValidateStatus(journey, command);

        var status = new JourneyStatusRecord
        {
            JourneyId = journey.Id,
            Status = command.Status,
            OccurredAt = command.OccurredAt,
            RecordedAt = acceptedAt,
            Source = ChangeSource.TransportProvider,
            Actor = provider.ClientId,
            IdempotencyKey = key,
            ExternalResourceCode = TransportMapping.Clean(command.ExternalResourceCode),
            Latitude = command.Latitude,
            Longitude = command.Longitude,
            CancellationReason = command.CancellationReason,
            CancellingParty = command.CancellingParty
        };
        journey.StatusHistory.Add(status);
        db.Add(status);
        JourneyMaterializer.Apply(journey);
        journey.ExternallyModified = true;
        await db.SaveChangesAsync(cancellationToken);
        return Success(status);
    }

    private IQueryable<TransportRequestRecord> Requests() =>
        db.TransportRequests.Include(x => x.JourneyRecords).ThenInclude(x => x.StatusHistory);

    private void Audit(string entityType, string identifier, string action, string actor, DateTimeOffset at) =>
        db.Add(new TransportAuditRecord
        {
            EntityType = entityType,
            EntityIdentifier = identifier,
            Action = action,
            Source = ChangeSource.TransportProvider,
            Actor = actor,
            RecordedAt = at
        });

    private static ProviderResourceSnapshot? Snapshot(TransportRequestRecord? owner, object snapshot) =>
        Authorization(owner) is { } authorization
            ? new ProviderResourceSnapshot(authorization.ProviderId, authorization.ContractCode, snapshot)
            : null;

    private static ProviderResourceAuthorization? Authorization(TransportRequestRecord? request) =>
        request?.ProviderId is { } providerId && !string.IsNullOrWhiteSpace(request.ContractCode)
            ? new ProviderResourceAuthorization(providerId, request.ContractCode)
            : null;

    private static ProviderCommandResult? AccessFailure(TransportRequestRecord? request, ProviderIdentity provider)
    {
        if (request is null) return Error(404, "Resource not found.");
        var authorization = Authorization(request);
        if (authorization is null || authorization.ProviderId != provider.ProviderId ||
            !provider.Contracts.Any(x => string.Equals(x, authorization.ContractCode, StringComparison.OrdinalIgnoreCase)))
            return Error(403, "The provider is not authorized for this resource.");
        return null;
    }

    private static T Required<T>(JsonElement payload) where T : class =>
        payload.Deserialize<T>(JsonOptions) ?? throw new JsonException("A command payload is required.");

    private static void ValidateLocations(LocationSnapshot origin, LocationSnapshot destination)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);
        if (origin.Type == LocationType.PrivateAddress && destination.Type == LocationType.PrivateAddress)
            throw new DomainValidationException("A private-to-private journey is not allowed.");
    }

    private static void ValidateStatus(JourneyRecord journey, AddJourneyStatusCommand command)
    {
        if (command.Latitude is < -90 or > 90 || command.Longitude is < -180 or > 180)
            throw new DomainValidationException("Coordinates are outside their valid ranges.");
        if (command.Status == JourneyStatus.Cancelled &&
            (command.CancellationReason is null || command.CancellingParty is null))
            throw new DomainValidationException("Cancellation reason and cancelling party are required.");
        if (command.Status != JourneyStatus.Cancelled &&
            (command.CancellationReason.HasValue || command.CancellingParty.HasValue))
            throw new DomainValidationException("Cancellation metadata is only valid for a cancelled status.");
        if (journey.CurrentStatus == JourneyStatus.Completed && command.OccurredAt >= journey.ActualCompletedAt)
            throw new DomainValidationException("A completed journey cannot be reopened.");
    }

    private static ProviderCommandResult Success(object value, int statusCode = 200) =>
        new(statusCode, JsonSerializer.Serialize(value, JsonOptions));

    private static ProviderCommandResult Error(int statusCode, string message) =>
        new(statusCode, JsonSerializer.Serialize(new { error = message }, JsonOptions));

    private sealed record JourneySnapshotPayload(
        LocationSnapshot Origin,
        LocationSnapshot Destination,
        TransportRequirements Requirements,
        SchedulePayload Schedule,
        string? ProviderVisibleNotes,
        string? ProviderReference)
    {
        internal JourneySnapshotCommand ToCommand(JourneyDirection direction) => new(
            Origin, Destination, Requirements, Schedule.ToDomain(direction, Destination),
            ProviderVisibleNotes, ProviderReference);
    }

    private sealed record ExceptionalJourneyPayload(
        JourneyDirection Direction,
        DateOnly ServiceDate,
        LocationSnapshot Origin,
        LocationSnapshot Destination,
        TransportRequirements Requirements,
        SchedulePayload Schedule,
        string? ProviderVisibleNotes,
        string? ProviderReference)
    {
        internal ExceptionalJourneyCommand ToCommand() => new(
            Direction, ServiceDate, Origin, Destination, Requirements,
            Schedule.ToDomain(Direction, Destination), ProviderVisibleNotes, ProviderReference);
    }

    private sealed record ExceptionalJourneyCommand(
        JourneyDirection Direction,
        DateOnly ServiceDate,
        LocationSnapshot Origin,
        LocationSnapshot Destination,
        TransportRequirements Requirements,
        JourneySchedule Schedule,
        string? ProviderVisibleNotes,
        string? ProviderReference);

    private sealed record SchedulePayload(
        DateTimeOffset? AppointmentAt,
        DateTimeOffset ScheduledStartAt,
        DateTimeOffset? ScheduledPickupAt,
        bool PickupTimePending)
    {
        internal JourneySchedule ToDomain(JourneyDirection direction, LocationSnapshot destination) =>
            direction == JourneyDirection.Return
                ? JourneySchedule.Return(ScheduledPickupAt ?? ScheduledStartAt, PickupTimePending)
                : JourneySchedule.Outbound(
                    AppointmentAt, destination.Type == LocationType.HealthcareFacility,
                    ScheduledStartAt, ScheduledPickupAt);
    }
}
