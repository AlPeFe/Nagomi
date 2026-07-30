using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ProviderIntegration;

public static class ProviderOperationsEndpoints
{
    public static async Task<IResult> QueryAsync(
        NotificationDeliveryState? state,
        bool? unretrieved,
        int? limit,
        IProviderIntegrationDb db,
        CancellationToken cancellationToken)
    {
        var query = db.ProviderNotifications.AsNoTracking();
        if (state.HasValue) query = query.Where(x => x.State == state);
        if (unretrieved is true)
            query = query.Where(x => x.State == NotificationDeliveryState.Published && x.RetrievedAt == null);
        var notifications = await query.OrderBy(x => x.CreatedAt)
            .Take(Math.Clamp(limit ?? 100, 1, 500)).ToListAsync(cancellationToken);
        return TypedResults.Ok(notifications);
    }

    public static async Task<IResult> RepublishAsync(
        Guid id, IProviderIntegrationDb db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var source = await db.ProviderNotifications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (source is null) return TypedResults.NotFound();
        if (source.State is not (NotificationDeliveryState.Dead or NotificationDeliveryState.Published))
            return TypedResults.Conflict(new { error = "Only dead or published-unretrieved notifications can be republished." });

        var now = timeProvider.GetUtcNow();
        var replacement = new ProviderNotification
        {
            CorrelationId = source.CorrelationId,
            ProviderId = source.ProviderId,
            ContractCode = source.ContractCode,
            MessageType = source.MessageType,
            EntityType = source.EntityType,
            EntityPublicId = source.EntityPublicId,
            RetrievalUrl = source.RetrievalUrl,
            CreatedAt = now,
            NextAttemptAt = now,
            ReplacesNotificationId = source.Id
        };
        db.ProviderNotifications.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/provider-integration/operations/notifications/{replacement.Id}", replacement);
    }
}
