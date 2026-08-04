using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.Api.Features.EmergencyTransports;

public sealed record IncidentLocationSubmission(
    decimal Latitude,
    decimal Longitude,
    string? Address = null,
    string? Municipality = null,
    string? Notes = null);

public sealed record CreateEmergencyTransportCommand(
    string Reason,
    IncidentLocationSubmission Incident,
    string? ContactPhone = null,
    string? Observations = null);

public static class EmergencyTransportEndpoints
{
    public static IEndpointRouteBuilder MapEmergencyTransportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/emergency-transports").WithTags("Emergency transports");
        group.MapPost("", Create);
        group.MapGet("", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/{id:guid}/cancel", Cancel);
        return endpoints;
    }

    private static async Task<Results<Created<EmergencyTransportRecord>, ValidationProblem>> Create(
        CreateEmergencyTransportCommand command,
        ITransportDb db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = clock.GetUtcNow();
            var incident = new IncidentLocation(
                command.Incident.Latitude,
                command.Incident.Longitude,
                command.Incident.Address,
                command.Incident.Municipality,
                command.Incident.Notes);
            var record = EmergencyTransportRecord.Create(
                command.Reason, incident, command.ContactPhone, command.Observations, now);
            db.Add(record);
            Audit(db, record.PublicId, "Created", ChangeSource.Nagomi, "simulated-user", now);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/api/emergency-transports/{record.Id}", record);
        }
        catch (DomainValidationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [exception.Message] });
        }
    }

    private static async Task<Ok<IReadOnlyList<EmergencyTransportRecord>>> List(
        ITransportDb db, CancellationToken cancellationToken)
    {
        var records = await db.EmergencyTransports.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<EmergencyTransportRecord>>(records);
    }

    private static async Task<Results<Ok<EmergencyTransportRecord>, NotFound>> Get(
        Guid id, ITransportDb db, CancellationToken cancellationToken)
    {
        var record = await db.EmergencyTransports.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return record is null ? TypedResults.NotFound() : TypedResults.Ok(record);
    }

    private static async Task<Results<Ok<EmergencyTransportRecord>, NotFound, Conflict<string>, ValidationProblem>> Cancel(
        Guid id, ITransportDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var record = await db.EmergencyTransports.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return TypedResults.NotFound();
        if (record.Status == EmergencyTransportStatus.Cancelled)
            return TypedResults.Conflict("The emergency transport is already cancelled.");
        if (record.Status == EmergencyTransportStatus.Completed)
            return TypedResults.Conflict("A completed emergency transport cannot be cancelled.");
        record.Status = EmergencyTransportStatus.Cancelled;
        record.UpdatedAt = clock.GetUtcNow();
        Audit(db, record.PublicId, "Cancelled", ChangeSource.Nagomi, "simulated-user", record.UpdatedAt);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(record);
    }

    private static void Audit(ITransportDb db, string id, string action, ChangeSource source, string actor, DateTimeOffset at) =>
        db.Add(new TransportAuditRecord
        {
            EntityType = "EmergencyTransport", EntityIdentifier = id, Action = action,
            Source = source, Actor = actor, RecordedAt = at
        });
}
