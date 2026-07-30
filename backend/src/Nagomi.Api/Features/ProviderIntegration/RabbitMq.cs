using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Nagomi.Api.Features.ProviderIntegration;

public sealed class ProviderRabbitMqOptions
{
    public const string SectionName = "ProviderIntegration:RabbitMq";
    public string Uri { get; set; } = "amqp://guest:guest@localhost:5672";
    public string Exchange { get; set; } = "nagomi.provider.notifications";
    public string DeadLetterExchange { get; set; } = "nagomi.provider.notifications.dead";
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int BatchSize { get; set; } = 50;
}

public interface IProviderNotificationPublisher
{
    Task PublishAsync(ProviderNotification notification, string queueName, CancellationToken cancellationToken);
}

public sealed class RabbitMqProviderNotificationPublisher(IOptions<ProviderRabbitMqOptions> options)
    : IProviderNotificationPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(
        ProviderNotification notification,
        string queueName,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var factory = new ConnectionFactory { Uri = new Uri(settings.Uri) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await channel.ExchangeDeclareAsync(settings.Exchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(settings.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = settings.DeadLetterExchange },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queueName, settings.Exchange, queueName, cancellationToken: cancellationToken);
        var deadQueueName = $"{queueName}.dead";
        await channel.QueueDeclareAsync(deadQueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(deadQueueName, settings.DeadLetterExchange, queueName,
            cancellationToken: cancellationToken);

        var message = new ProviderNotificationMessage(
            notification.MessageId,
            notification.MessageType,
            notification.EntityPublicId,
            notification.ContractCode,
            notification.CreatedAt,
            notification.RetrievalUrl);
        var body = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = notification.MessageId.ToString("D"),
            CorrelationId = notification.CorrelationId.ToString("D"),
            ContentType = "application/json",
            Type = notification.MessageType
        };
        await channel.BasicPublishAsync(settings.Exchange, queueName, mandatory: true, properties, body, cancellationToken);
    }
}

public sealed class ProviderOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IProviderNotificationPublisher publisher,
    IOptions<ProviderRabbitMqOptions> options,
    TimeProvider timeProvider,
    ILogger<ProviderOutboxWorker> logger) : BackgroundService
{
    public const int MaximumRetries = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);
        do
        {
            await PublishBatchAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IProviderIntegrationDb>();
        var now = timeProvider.GetUtcNow();
        var notifications = await db.ProviderNotifications
            .Where(x => x.State == NotificationDeliveryState.Pending &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            var queueName = await db.TransportProviders.AsNoTracking()
                .Where(x => x.Id == notification.ProviderId && x.IsActive)
                .Select(x => x.QueueName)
                .SingleOrDefaultAsync(cancellationToken);
            if (queueName is null)
            {
                MarkFailure(notification, now, "provider-inactive");
                continue;
            }

            try
            {
                await publisher.PublishAsync(notification, queueName, cancellationToken);
                notification.State = NotificationDeliveryState.Published;
                notification.PublishedAt = now;
                notification.NextAttemptAt = null;
                notification.LastFailureCode = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                MarkFailure(notification, now, "publish-failed");
                logger.LogWarning(exception,
                    "Provider notification publication failed for message {MessageId}; attempt {Attempt}",
                    notification.MessageId, notification.FailedPublishAttempts);
            }
        }

        if (notifications.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private void MarkFailure(ProviderNotification notification, DateTimeOffset now, string code)
    {
        notification.FailedPublishAttempts++;
        notification.LastFailureCode = code;
        if (notification.FailedPublishAttempts > MaximumRetries)
        {
            notification.State = NotificationDeliveryState.Dead;
            notification.NextAttemptAt = null;
        }
        else
        {
            notification.NextAttemptAt = now + options.Value.RetryDelay;
        }
    }
}
