using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ProviderIntegration;

public interface IProviderOutbox
{
    Task<ProviderNotification?> AddAsync(
        string contractCode,
        string messageType,
        IntegrationEntityType entityType,
        string entityPublicId,
        string retrievalPath,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderOutbox(IProviderIntegrationDb db, TimeProvider timeProvider) : IProviderOutbox
{
    public async Task<ProviderNotification?> AddAsync(
        string contractCode,
        string messageType,
        IntegrationEntityType entityType,
        string entityPublicId,
        string retrievalPath,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPublicId);
        ArgumentException.ThrowIfNullOrWhiteSpace(retrievalPath);

        var route = await db.ProviderContractRoutes.AsNoTracking()
            .Where(x => x.IsActive && x.Contract.IsActive && x.Provider.IsActive && x.Contract.Code == contractCode)
            .Select(x => new { x.ProviderId })
            .SingleOrDefaultAsync(cancellationToken);
        if (route is null)
            return null;

        var now = timeProvider.GetUtcNow();
        var notification = new ProviderNotification
        {
            CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId,
            ProviderId = route.ProviderId,
            ContractCode = contractCode.Trim(),
            MessageType = messageType.Trim(),
            EntityType = entityType,
            EntityPublicId = entityPublicId.Trim(),
            RetrievalUrl = retrievalPath.Trim(),
            CreatedAt = now,
            NextAttemptAt = now
        };
        db.ProviderNotifications.Add(notification);
        return notification;
    }
}

public interface INotificationRetrievalTracker
{
    Task MarkRetrievedAsync(Guid messageId, Guid providerId, string entityPublicId, CancellationToken cancellationToken);
}

public sealed class NotificationRetrievalTracker(IProviderIntegrationDb db, TimeProvider timeProvider)
    : INotificationRetrievalTracker
{
    public async Task MarkRetrievedAsync(
        Guid messageId,
        Guid providerId,
        string entityPublicId,
        CancellationToken cancellationToken)
    {
        var notification = await db.ProviderNotifications.SingleOrDefaultAsync(
            x => x.MessageId == messageId && x.ProviderId == providerId && x.EntityPublicId == entityPublicId,
            cancellationToken);
        if (notification is null || notification.State == NotificationDeliveryState.Retrieved)
            return;

        notification.State = NotificationDeliveryState.Retrieved;
        notification.RetrievedAt = timeProvider.GetUtcNow();
        notification.RetrievedByProviderId = providerId;
        await db.SaveChangesAsync(cancellationToken);
    }
}
