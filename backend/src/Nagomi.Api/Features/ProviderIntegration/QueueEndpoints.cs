using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.ProviderIntegration;

public static class QueueEndpoints
{
    public static IEndpointRouteBuilder MapWebQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/queue")
            .WithTags("Provider queue")
            .RequireAuthorization(UserAuthorizationPolicies.Web);
        group.MapGet("", GetSnapshotsAsync);
        group.MapGet("/{queueName}/peek", PeekAsync);
        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<QueueSnapshot>>> GetSnapshotsAsync(
        IProviderQueueInspector inspector, CancellationToken cancellationToken)
    {
        var snapshots = await inspector.GetQueueSnapshotsAsync(cancellationToken);
        return TypedResults.Ok(snapshots);
    }

    private static async Task<Results<Ok<IReadOnlyList<QueueMessageSample>>, NotFound>> PeekAsync(
        [FromRoute] string queueName, int limit, IProviderQueueInspector inspector, CancellationToken cancellationToken)
    {
        // Validate the queue belongs to an active provider before peeking.
        try
        {
            var samples = await inspector.PeekAsync(queueName, limit, cancellationToken);
            return TypedResults.Ok(samples);
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
        {
            return TypedResults.NotFound();
        }
    }
}
