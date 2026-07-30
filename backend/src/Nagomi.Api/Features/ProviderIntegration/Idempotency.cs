using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ProviderIntegration;

public sealed record ProviderCommandResult(int StatusCode, string? Body);

public sealed record IdempotentCommandResult(bool IsReplay, ProviderCommandResult Result);

public interface IProviderCommandIdempotency
{
    Task<IdempotentCommandResult> ExecuteAsync(
        ProviderIdentity provider,
        string idempotencyKey,
        string commandType,
        string entityPublicId,
        string canonicalRequest,
        Guid correlationId,
        Func<CancellationToken, Task<ProviderCommandResult>> command,
        CancellationToken cancellationToken);
}

public sealed class ProviderCommandIdempotency(IProviderIntegrationDb db, TimeProvider timeProvider)
    : IProviderCommandIdempotency
{
    public async Task<IdempotentCommandResult> ExecuteAsync(
        ProviderIdentity provider,
        string idempotencyKey,
        string commandType,
        string entityPublicId,
        string canonicalRequest,
        Guid correlationId,
        Func<CancellationToken, Task<ProviderCommandResult>> command,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var key = idempotencyKey.Trim();
        var hash = HashCanonicalJson(canonicalRequest);
        var prior = await db.ProviderCommandReceipts.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProviderId == provider.ProviderId && x.IdempotencyKey == key,
            cancellationToken);
        if (prior is not null)
        {
            if (!string.Equals(prior.CommandType, commandType, StringComparison.Ordinal) ||
                !string.Equals(prior.EntityPublicId, entityPublicId, StringComparison.Ordinal) ||
                !string.Equals(prior.RequestHash, hash, StringComparison.Ordinal))
                throw new IdempotencyConflictException();

            return new(true, new ProviderCommandResult(prior.ResponseStatusCode, prior.ResponseBody));
        }

        var result = await command(cancellationToken);
        db.ProviderCommandReceipts.Add(new ProviderCommandReceipt
        {
            ProviderId = provider.ProviderId,
            IdempotencyKey = key,
            CommandType = commandType,
            EntityPublicId = entityPublicId,
            RequestHash = hash,
            ReceivedAt = timeProvider.GetUtcNow(),
            ResponseStatusCode = result.StatusCode,
            ResponseBody = result.Body,
            CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId
        });
        await db.SaveChangesAsync(cancellationToken);
        return new(false, result);
    }

    public static string HashCanonicalJson(string request)
    {
        using var document = JsonDocument.Parse(request);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(document.RootElement, writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(item, writer);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}

public sealed class IdempotencyConflictException()
    : InvalidOperationException("The idempotency key was already used for a different command.");
