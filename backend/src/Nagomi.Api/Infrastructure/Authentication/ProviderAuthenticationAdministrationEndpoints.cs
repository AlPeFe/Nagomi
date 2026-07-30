using System.Security.Cryptography;
using System.Text.Json;
using Nagomi.Api.Features.ProviderIntegration;
using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nagomi.Api.Infrastructure.Authentication;

public sealed record CreateProviderAuthenticationClientRequest(
    string ClientId,
    Guid ProviderId,
    string[] Contracts,
    string Role,
    string? ClientSecret = null,
    string? DisplayName = null);

public sealed record RotateProviderAuthenticationClientSecretRequest(string? ClientSecret = null);

public sealed record ProviderAuthenticationClientSecretResponse(
    string ClientId,
    string ClientSecret,
    Guid? ProviderId = null,
    IReadOnlyList<string>? Contracts = null,
    string? Role = null);

public static class ProviderAuthenticationAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapProviderAuthenticationAdministrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var administration = endpoints.MapGroup("/api/provider-administration/clients")
            .WithTags("Provider authentication administration")
            .RequireAuthorization(ProviderAuthorizationPolicies.Administration);
        administration.MapPost("", CreateClientAsync);
        administration.MapPost("/{clientId}/rotate-secret", RotateSecretAsync);
        administration.MapDelete("/{clientId}", RevokeClientAsync);
        return endpoints;
    }

    public static async Task<IResult> CreateClientAsync(
        CreateProviderAuthenticationClientRequest request,
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var clientId = request.ClientId.Trim();
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is not null)
            return TypedResults.Conflict(new { error = $"Client '{clientId}' already exists." });

        var secret = Secret(request.ClientSecret);
        var contracts = request.Contracts.Select(NormalizeContract)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var role = request.Role.Trim().ToLowerInvariant();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            ClientType = ClientTypes.Confidential,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? clientId : request.DisplayName.Trim()
        };
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        descriptor.Properties[ProviderClientProperties.ProviderId] =
            JsonSerializer.SerializeToElement(request.ProviderId.ToString());
        descriptor.Properties[ProviderClientProperties.Role] = JsonSerializer.SerializeToElement(role);
        descriptor.Properties[ProviderClientProperties.Contracts] = JsonSerializer.SerializeToElement(contracts);

        await applications.CreateAsync(descriptor, cancellationToken);
        return TypedResults.Created(
            $"/api/provider-administration/clients/{Uri.EscapeDataString(clientId)}",
            new ProviderAuthenticationClientSecretResponse(clientId, secret, request.ProviderId, contracts, role));
    }

    public static async Task<IResult> RotateSecretAsync(
        string clientId,
        RotateProviderAuthenticationClientSecretRequest request,
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
        return TypedResults.Ok(new ProviderAuthenticationClientSecretResponse(clientId, secret));
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

    private static Dictionary<string, string[]> Validate(CreateProviderAuthenticationClientRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ClientId))
            errors["clientId"] = ["Client ID is required."];
        if (request.ProviderId == Guid.Empty)
            errors["providerId"] = ["Provider ID is required."];
        if (request.Contracts is null || request.Contracts.Length == 0 || request.Contracts.Any(string.IsNullOrWhiteSpace))
            errors["contracts"] = ["At least one non-empty contract is required."];
        if (request.Role?.Trim().ToLowerInvariant() is not
            (ProviderClaimTypes.Administrator or ProviderClaimTypes.Operator))
            errors["role"] = ["Role must be 'administrator' or 'operator'."];
        if (request.ClientSecret is not null && string.IsNullOrWhiteSpace(request.ClientSecret))
            errors["clientSecret"] = ["A supplied client secret cannot be empty."];
        return errors;
    }

    private static string NormalizeContract(string contract) => contract.Trim().ToUpperInvariant();

    private static string Secret(string? supplied) => supplied is null
        ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_')
        : supplied;

    private static IResult Invalid(string name, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [name] = [message] });
}
