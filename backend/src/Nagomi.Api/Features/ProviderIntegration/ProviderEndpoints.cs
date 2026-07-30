using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Nagomi.Api.Features.ProviderIntegration;

public sealed record ProviderResourceAuthorization(Guid ProviderId, string ContractCode);
public sealed record ProviderResourceSnapshot(Guid ProviderId, string ContractCode, object Snapshot);

public interface IProviderResourceGateway
{
    Task<ProviderResourceSnapshot?> GetRequestAsync(string publicId, CancellationToken cancellationToken);
    Task<ProviderResourceSnapshot?> GetJourneyAsync(string publicId, CancellationToken cancellationToken);
    Task<ProviderResourceAuthorization?> GetRequestAuthorizationAsync(string publicId, CancellationToken cancellationToken);
    Task<ProviderResourceAuthorization?> GetJourneyAuthorizationAsync(string publicId, CancellationToken cancellationToken);
    Task<ProviderCommandResult> ExecuteAsync(
        string commandType,
        string entityPublicId,
        JsonElement payload,
        ProviderIdentity provider,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken);
}

public static class ProviderEndpoints
{
    public static IEndpointRouteBuilder MapProviderIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var provider = endpoints.MapGroup("/api/provider").WithTags("Provider integration").RequireAuthorization();
        provider.MapGet("/requests/{publicId}", GetRequestAsync);
        provider.MapGet("/journeys/{publicId}", GetJourneyAsync);
        provider.MapPut("/requests/{publicId}", ReplaceRequestAsync);
        provider.MapPut("/journeys/{publicId}", ReplaceJourneyAsync);
        provider.MapPost("/requests/{publicId}/journeys", AddExceptionalJourneyAsync);
        provider.MapPost("/requests/{publicId}/cancel", CancelRequestAsync);
        provider.MapPost("/journeys/{publicId}/cancel", CancelJourneyAsync);
        provider.MapPost("/journeys/{publicId}/status", AddJourneyStatusAsync);

        var administration = endpoints.MapGroup("/api/provider-administration")
            .WithTags("Provider administration")
            .RequireAuthorization(ProviderAuthorizationPolicies.Administration);
        administration.MapGet("/providers", ProviderAdministrationEndpoints.GetProvidersAsync);
        administration.MapPost("/providers", ProviderAdministrationEndpoints.CreateProviderAsync);
        administration.MapPut("/providers/{id:guid}", ProviderAdministrationEndpoints.UpdateProviderAsync);
        administration.MapGet("/contracts", ProviderAdministrationEndpoints.GetContractsAsync);
        administration.MapPost("/contracts", ProviderAdministrationEndpoints.CreateContractAsync);
        administration.MapPut("/contracts/{id:guid}", ProviderAdministrationEndpoints.UpdateContractAsync);
        administration.MapPost("/contracts/{contractId:guid}/route", ProviderAdministrationEndpoints.SetRouteAsync);

        var operations = endpoints.MapGroup("/api/provider-integration/operations")
            .WithTags("Provider integration operations")
            .RequireAuthorization(ProviderAuthorizationPolicies.Operations);
        operations.MapGet("/notifications", ProviderOperationsEndpoints.QueryAsync);
        operations.MapPost("/notifications/{id:guid}/republish", ProviderOperationsEndpoints.RepublishAsync);
        return endpoints;
    }

    public static Task<IResult> ReplaceRequestAsync(
        string publicId, JsonElement payload, HttpContext context, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, IProviderCommandIdempotency idempotency, TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync("request.replace", publicId, payload, false, context, gateway, authorizer,
            idempotency, timeProvider, cancellationToken);

    public static Task<IResult> ReplaceJourneyAsync(
        string publicId, JsonElement payload, HttpContext context, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, IProviderCommandIdempotency idempotency, TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync("journey.replace", publicId, payload, true, context, gateway, authorizer,
            idempotency, timeProvider, cancellationToken);

    public static Task<IResult> AddExceptionalJourneyAsync(
        string publicId, JsonElement payload, HttpContext context, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, IProviderCommandIdempotency idempotency, TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync("request.journey.add", publicId, payload, false, context, gateway, authorizer,
            idempotency, timeProvider, cancellationToken);

    public static Task<IResult> CancelRequestAsync(
        string publicId, JsonElement payload, HttpContext context, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, IProviderCommandIdempotency idempotency, TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync("request.cancel", publicId, payload, false, context, gateway, authorizer,
            idempotency, timeProvider, cancellationToken);

    public static Task<IResult> CancelJourneyAsync(
        string publicId, JsonElement payload, HttpContext context, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, IProviderCommandIdempotency idempotency, TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync("journey.cancel", publicId, payload, true, context, gateway, authorizer,
            idempotency, timeProvider, cancellationToken);

    public static Task<IResult> AddJourneyStatusAsync(
        string publicId, JsonElement payload, HttpContext context, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, IProviderCommandIdempotency idempotency, TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        ExecuteWriteAsync("journey.status", publicId, payload, true, context, gateway, authorizer,
            idempotency, timeProvider, cancellationToken);

    public static async Task<IResult> GetRequestAsync(
        string publicId, Guid? messageId, ClaimsPrincipal user, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, INotificationRetrievalTracker tracker, CancellationToken cancellationToken)
    {
        var resource = await gateway.GetRequestAsync(publicId, cancellationToken);
        return await CompleteRetrievalAsync(resource, publicId, messageId, user, authorizer, tracker, cancellationToken);
    }

    public static async Task<IResult> GetJourneyAsync(
        string publicId, Guid? messageId, ClaimsPrincipal user, IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer, INotificationRetrievalTracker tracker, CancellationToken cancellationToken)
    {
        var resource = await gateway.GetJourneyAsync(publicId, cancellationToken);
        return await CompleteRetrievalAsync(resource, publicId, messageId, user, authorizer, tracker, cancellationToken);
    }

    private static async Task<IResult> CompleteRetrievalAsync(
        ProviderResourceSnapshot? resource,
        string publicId,
        Guid? messageId,
        ClaimsPrincipal user,
        IProviderAuthorizer authorizer,
        INotificationRetrievalTracker tracker,
        CancellationToken cancellationToken)
    {
        if (resource is null)
            return TypedResults.NotFound();
        var authorization = authorizer.Authorize(user, resource.ProviderId, resource.ContractCode);
        if (!authorization.Succeeded)
            return AuthorizationFailure(authorization.Failure);
        if (messageId.HasValue)
            await tracker.MarkRetrievedAsync(messageId.Value, authorization.Identity!.ProviderId, publicId, cancellationToken);
        return TypedResults.Ok(resource.Snapshot);
    }

    private static async Task<IResult> ExecuteWriteAsync(
        string commandType,
        string publicId,
        JsonElement payload,
        bool journey,
        HttpContext context,
        IProviderResourceGateway gateway,
        IProviderAuthorizer authorizer,
        IProviderCommandIdempotency idempotency,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var resource = journey
            ? await gateway.GetJourneyAuthorizationAsync(publicId, cancellationToken)
            : await gateway.GetRequestAuthorizationAsync(publicId, cancellationToken);
        if (resource is null)
            return TypedResults.NotFound();
        var authorization = authorizer.Authorize(context.User, resource.ProviderId, resource.ContractCode);
        if (!authorization.Succeeded)
            return AuthorizationFailure(authorization.Failure);
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var values) ||
            string.IsNullOrWhiteSpace(values.ToString()))
            return TypedResults.BadRequest(new { error = "An Idempotency-Key header is required." });

        try
        {
            var correlationId = Guid.TryParse(context.TraceIdentifier, out var parsed) ? parsed : Guid.NewGuid();
            var result = await idempotency.ExecuteAsync(
                authorization.Identity!, values.ToString(), commandType, publicId, payload.GetRawText(), correlationId,
                token => gateway.ExecuteAsync(commandType, publicId, payload, authorization.Identity!,
                    timeProvider.GetUtcNow(), token), cancellationToken);
            if (result.IsReplay)
                context.Response.Headers["Idempotent-Replay"] = "true";
            return Results.Content(result.Result.Body, "application/json", statusCode: result.Result.StatusCode);
        }
        catch (IdempotencyConflictException)
        {
            return TypedResults.Conflict(new { error = "The idempotency key was already used for another command." });
        }
    }

    private static IResult AuthorizationFailure(ProviderAuthorizationFailure failure) =>
        failure == ProviderAuthorizationFailure.Unauthenticated
            ? TypedResults.Unauthorized()
            : TypedResults.Forbid();
}
