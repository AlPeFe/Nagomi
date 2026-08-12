using System.Security.Cryptography;
using System.Text.Json;
using Nagomi.Api.Infrastructure.Authentication;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nagomi.Api.Features.IdentityAdministration;

public sealed record CreateApiClientRequest(
    string ClientId,
    string? DisplayName = null,
    string? ClientSecret = null);

public sealed record RotateApiClientSecretRequest(string? ClientSecret = null);

public sealed record ApiClientRow(
    string ClientId,
    string? DisplayName,
    bool IsConfidential,
    IReadOnlyList<string> Permissions);

public sealed record ApiClientSecretResponse(string ClientId, string ClientSecret);

/// <summary>
/// Central identity & access management for machine-to-machine consumers. Every
/// endpoint is administrator-only (see the identity-access-management spec).
/// </summary>
public static class IdentityAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapIdentityAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/identity/clients")
            .WithTags("Identity & access management (API clients)")
            .RequireAuthorization(UserAuthorizationPolicies.Admin);
        group.MapGet("", ListClientsAsync);
        group.MapPost("", CreateClientAsync);
        group.MapPost("/{clientId}/rotate-secret", RotateSecretAsync);
        group.MapDelete("/{clientId}", RevokeClientAsync);
        return endpoints;
    }

    public static async Task<IResult> ListClientsAsync(
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken)
    {
        var rows = new List<ApiClientRow>();
        await foreach (var application in applications.ListAsync())
        {
            if (!await IsApiConsumerAsync(application, applications, cancellationToken))
                continue;
            rows.Add(new ApiClientRow(
                await applications.GetClientIdAsync(application, cancellationToken) ?? string.Empty,
                await applications.GetDisplayNameAsync(application, cancellationToken),
                await applications.GetClientTypeAsync(application, cancellationToken) == ClientTypes.Confidential,
                await applications.GetPermissionsAsync(application, cancellationToken)));
        }
        return TypedResults.Ok(rows);
    }

    public static async Task<IResult> CreateClientAsync(
        CreateApiClientRequest request,
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ClientId))
            errors["clientId"] = ["Client ID is required."];
        if (request.ClientSecret is not null && string.IsNullOrWhiteSpace(request.ClientSecret))
            errors["clientSecret"] = ["A supplied client secret cannot be empty."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var clientId = request.ClientId.Trim();
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is not null)
            return TypedResults.Conflict(new { error = $"Client '{clientId}' already exists." });

        var secret = Secret(request.ClientSecret);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            ClientType = ClientTypes.Confidential,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? clientId : request.DisplayName.Trim()
        };
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        descriptor.Properties[ProviderClientProperties.ApiConsumer] = JsonSerializer.SerializeToElement(true);

        await applications.CreateAsync(descriptor, cancellationToken);
        return TypedResults.Created(
            $"/api/admin/identity/clients/{Uri.EscapeDataString(clientId)}",
            new ApiClientSecretResponse(clientId, secret));
    }

    public static async Task<IResult> RotateSecretAsync(
        string clientId,
        RotateApiClientSecretRequest request,
        IOpenIddictApplicationManager applications,
        IOpenIddictTokenManager tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return Invalid("clientId", "Client ID is required.");
        if (request.ClientSecret is not null && string.IsNullOrWhiteSpace(request.ClientSecret))
            return Invalid("clientSecret", "A supplied client secret cannot be empty.");

        var application = await applications.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
            return TypedResults.NotFound();

        var secret = Secret(request.ClientSecret);
        await applications.UpdateAsync(application, secret, cancellationToken);
        await RevokeTokensAsync(application, applications, tokens, cancellationToken);
        return TypedResults.Ok(new ApiClientSecretResponse(clientId, secret));
    }

    public static async Task<IResult> RevokeClientAsync(
        string clientId,
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return Invalid("clientId", "Client ID is required.");

        var application = await applications.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
            return TypedResults.NotFound();

        await applications.DeleteAsync(application, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<bool> IsApiConsumerAsync(
        object application,
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken)
    {
        var properties = await applications.GetPropertiesAsync(application, cancellationToken);
        return properties.TryGetValue(ProviderClientProperties.ApiConsumer, out var marker)
            && marker.ValueKind is JsonValueKind.True;
    }

    private static async Task RevokeTokensAsync(
        object application,
        IOpenIddictApplicationManager applications,
        IOpenIddictTokenManager tokens,
        CancellationToken cancellationToken)
    {
        var applicationId = await applications.GetIdAsync(application, cancellationToken)
            ?? throw new InvalidOperationException("The OpenIddict application has no identifier.");
        await tokens.RevokeByApplicationIdAsync(applicationId, cancellationToken);
    }

    private static string Secret(string? supplied) => supplied is null
        ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_')
        : supplied;

    private static IResult Invalid(string name, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [name] = [message] });
}
