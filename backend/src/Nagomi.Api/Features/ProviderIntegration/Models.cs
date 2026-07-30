namespace Nagomi.Api.Features.ProviderIntegration;

public sealed class TransportProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string QueueName { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public sealed class TransportContract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public sealed class ProviderContractRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProviderId { get; set; }
    public Guid ContractId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public TransportProvider Provider { get; set; } = null!;
    public TransportContract Contract { get; set; } = null!;
}

public enum IntegrationEntityType
{
    TransportRequest,
    Journey
}

public enum NotificationDeliveryState
{
    Pending,
    Published,
    Retrieved,
    Dead
}

public sealed class ProviderNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public Guid ProviderId { get; set; }
    public string ContractCode { get; set; } = null!;
    public string MessageType { get; set; } = null!;
    public IntegrationEntityType EntityType { get; set; }
    public string EntityPublicId { get; set; } = null!;
    public string RetrievalUrl { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public NotificationDeliveryState State { get; set; } = NotificationDeliveryState.Pending;
    public int FailedPublishAttempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? RetrievedAt { get; set; }
    public Guid? RetrievedByProviderId { get; set; }
    public Guid? ReplacesNotificationId { get; set; }
    public string? LastFailureCode { get; set; }
}

public sealed class ProviderCommandReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProviderId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string CommandType { get; set; } = null!;
    public string EntityPublicId { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public DateTimeOffset ReceivedAt { get; set; }
    public int ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed record ProviderNotificationMessage(
    Guid MessageId,
    string MessageType,
    string EntityPublicId,
    string ContractCode,
    DateTimeOffset Timestamp,
    string RetrievalUrl);
