using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Nagomi.Api.Features.ProviderIntegration;

public sealed record QueueSnapshot(
    Guid ProviderId,
    string ProviderCode,
    string ProviderName,
    string QueueName,
    int Messages,
    int MessagesReady,
    int MessagesUnacknowledged,
    int Consumers,
    string? Error);
public sealed record QueueMessageSample(
    ulong DeliveryTag,
    string MessageId,
    string MessageType,
    string? EntityPublicId,
    string? ContractCode,
    bool Redelivered,
    string Body,
    DateTimeOffset? Timestamp);

/// <summary>Read-only view of the messages currently held in a provider queue.</summary>
public interface IProviderQueueInspector
{
    Task<IReadOnlyList<QueueSnapshot>> GetQueueSnapshotsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<QueueMessageSample>> PeekAsync(string queueName, int limit, CancellationToken cancellationToken);
}

public sealed class RabbitMqProviderQueueInspector(
    IProviderIntegrationDb db,
    IOptions<ProviderRabbitMqOptions> options) : IProviderQueueInspector
{
    private const int DefaultPeekLimit = 10;

    public async Task<IReadOnlyList<QueueSnapshot>> GetQueueSnapshotsAsync(CancellationToken cancellationToken)
    {
        var providers = await db.TransportProviders.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var result = new List<QueueSnapshot>(providers.Count);
        foreach (var provider in providers)
        {
            var snapshot = await SnapshotAsync(provider, cancellationToken);
            result.Add(snapshot);
        }
        return result;
    }

    private async Task<QueueSnapshot> SnapshotAsync(TransportProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var factory = new ConnectionFactory { Uri = new Uri(settings.Uri) };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            var declareOk = await channel.QueueDeclarePassiveAsync(provider.QueueName, cancellationToken);

            return new QueueSnapshot(
                provider.Id, provider.Code, provider.Name, provider.QueueName,
                (int)declareOk.MessageCount, (int)declareOk.MessageCount, 0, (int)declareOk.ConsumerCount, null);
        }
        catch (Exception exception)
        {
            return new QueueSnapshot(
                provider.Id, provider.Code, provider.Name, provider.QueueName,
                0, 0, 0, 0, exception.Message);
        }
    }

    public async Task<IReadOnlyList<QueueMessageSample>> PeekAsync(
        string queueName, int limit, CancellationToken cancellationToken)
    {
        var requested = Math.Clamp(limit <= 0 ? DefaultPeekLimit : limit, 1, 50);
        var settings = options.Value;
        var factory = new ConnectionFactory { Uri = new Uri(settings.Uri) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);

        // BasicGet + Nack(requeue) reinserts the message at the head of the queue, so a
        // get/requeue loop would return the SAME message repeatedly. To peek N DISTINCT
        // messages non-destructively, use a consumer with prefetch: Rabbit delivers up to
        // `requested` distinct messages into the consumer's buffer, we read them all, then
        // reject each with requeue:true so nothing is consumed.
        var samples = new List<QueueMessageSample>(requested);
        var countdown = new SemaphoreSlim(0, requested);
        var consumer = new AsyncEventingBasicConsumer(channel);
        var deliveries = new List<(ulong DeliveryTag, ReadOnlyMemory<byte> Body, IReadOnlyBasicProperties? Props, bool Redelivered)>(requested);

        consumer.ReceivedAsync += (_, args) =>
        {
            lock (deliveries)
            {
                if (deliveries.Count < requested)
                    deliveries.Add((args.DeliveryTag, args.Body, args.BasicProperties, args.Redelivered));
            }
            countdown.Release();
            return Task.CompletedTask;
        };

        await channel.BasicQosAsync(0, (ushort)requested, global: false, cancellationToken);
        var consumerTag = await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
        try
        {
            // Wait until we have `requested` deliveries or the queue drains (no new message
            // within the grace window).
            var drained = false;
            while (deliveries.Count < requested && !drained)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
                try
                {
                    await countdown.WaitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    drained = true; // no more messages arriving quickly enough
                }
            }

            foreach (var (tag, body, props, redelivered) in deliveries)
            {
                var text = body.IsEmpty ? string.Empty : Encoding.UTF8.GetString(body.Span);
                var timestamp = props?.Timestamp is { } ts ? DateTimeOffset.FromUnixTimeSeconds(ts.UnixTime) : (DateTimeOffset?)null;
                var (entityPublicId, contractCode) = ExtractMetadata(text);
                samples.Add(new QueueMessageSample(
                    tag,
                    props?.MessageId ?? string.Empty,
                    props?.Type ?? string.Empty,
                    entityPublicId, contractCode, redelivered, text, timestamp));
                // Requeue each distinct message so the peek is non-destructive.
                await channel.BasicNackAsync(tag, multiple: false, requeue: true, cancellationToken);
            }
        }
        finally
        {
            try { await channel.BasicCancelAsync(consumerTag, cancellationToken: cancellationToken); } catch { /* best-effort */ }
        }

        return samples;
    }

    private static (string? EntityPublicId, string? ContractCode) ExtractMetadata(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (null, null);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var entity = root.TryGetProperty("entityPublicId", out var e) ? e.GetString() : null;
            var contract = root.TryGetProperty("contractCode", out var c) ? c.GetString() : null;
            return (entity, contract);
        }
        catch
        {
            return (null, null);
        }
    }
}
