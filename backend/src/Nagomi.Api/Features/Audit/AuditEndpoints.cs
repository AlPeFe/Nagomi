using Microsoft.AspNetCore.Http.HttpResults;

namespace Nagomi.Api.Features.Audit;

public interface IAuditHistoryQuery
{
    Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(
        string entityType,
        string entityIdentifier,
        CancellationToken cancellationToken);
}

public sealed record AuditEntryResponse(
    Guid Id,
    string EntityType,
    string EntityIdentifier,
    AuditAction Action,
    AuditSource Source,
    string ActorIdentifier,
    string ActorDisplayName,
    string? ProviderIdentifier,
    string? ProviderName,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<AuditChangeResponse> Changes);

public sealed record AuditChangeResponse(
    string FieldName,
    string? PreviousValue,
    string? CurrentValue,
    AuditValueProtection Protection);

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/audit/{entityType}/{entityIdentifier}",
                GetHistoryAsync)
            .WithName("GetEntityAuditHistory")
            .WithTags("Audit");

        return endpoints;
    }

    public static async Task<Ok<IReadOnlyList<AuditEntryResponse>>> GetHistoryAsync(
        string entityType,
        string entityIdentifier,
        IAuditHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var entries = await query.GetHistoryAsync(entityType, entityIdentifier, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<AuditEntryResponse>>(entries.Select(ToResponse).ToArray());
    }

    public static AuditEntryResponse ToResponse(AuditEntry entry) =>
        new(
            entry.Id,
            entry.EntityType,
            entry.EntityIdentifier,
            entry.Action,
            entry.Source,
            entry.ActorIdentifier,
            entry.ActorDisplayName,
            entry.ProviderIdentifier,
            entry.ProviderName,
            entry.ReceivedAt,
            entry.Changes.Select(change => new AuditChangeResponse(
                change.FieldName,
                change.PreviousValue,
                change.CurrentValue,
                change.Protection)).ToArray());
}
