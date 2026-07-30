using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nagomi.Api.Features.ProviderIntegration;
using OpenIddict.Abstractions;
using OpenIddict.Server;

using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Nagomi.Api.Infrastructure.Authentication;

internal static class ProviderClientProperties
{
    public const string ProviderId = "nagomi:provider_id";
    public const string Role = "nagomi:role";
    public const string Contracts = "nagomi:contracts";
}

internal sealed class ProviderClientCredentialsHandler(IOpenIddictApplicationManager applications)
    : IOpenIddictServerHandler<HandleTokenRequestContext>
{
    public async ValueTask HandleAsync(HandleTokenRequestContext context)
    {
        if (!context.Request.IsClientCredentialsGrantType())
            return;

        var clientId = context.Request.ClientId!;
        var application = await applications.FindByClientIdAsync(clientId, context.CancellationToken)
            ?? throw new InvalidOperationException("The authenticated OpenIddict application no longer exists.");
        var properties = await applications.GetPropertiesAsync(application, context.CancellationToken);

        var providerId = GetRequiredString(properties, ProviderClientProperties.ProviderId);
        var role = GetRequiredString(properties, ProviderClientProperties.Role);
        var contracts = GetRequiredStrings(properties, ProviderClientProperties.Contracts);

        var identity = new ClaimsIdentity("OpenIddict.Server");
        identity.AddClaim(new Claim(Claims.Subject, clientId));
        identity.AddClaim(new Claim(Claims.ClientId, clientId));
        identity.AddClaim(new Claim(ProviderClaimTypes.ProviderId, providerId));
        identity.AddClaim(new Claim(ProviderClaimTypes.Role, role));
        identity.AddClaims(contracts.Select(contract => new Claim(ProviderClaimTypes.Contract, contract)));

        var principal = new ClaimsPrincipal(identity);
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject or Claims.ClientId or ProviderClaimTypes.ProviderId or
                ProviderClaimTypes.Role or ProviderClaimTypes.Contract => [Destinations.AccessToken],
            _ => []
        });
        context.SignIn(principal);
    }

    private static string GetRequiredString(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name) =>
        properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"OpenIddict application property '{name}' is missing.");

    private static string[] GetRequiredStrings(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name) =>
        properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray()
            : throw new InvalidOperationException($"OpenIddict application property '{name}' is missing.");
}

internal sealed class ProviderClientBootstrapService(
    IServiceProvider services,
    IOptions<ProviderAuthenticationOptions> options,
    ILogger<ProviderClientBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = options.Value.BootstrapClient;
        if (client is not { Enabled: true })
            return;

        Validate(client);

        await using var scope = services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        if (await applications.FindByClientIdAsync(client.ClientId!, cancellationToken) is not null)
        {
            logger.LogInformation("Provider integration client {ClientId} already exists; bootstrap left it unchanged.", client.ClientId);
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId!.Trim(),
            ClientSecret = client.ClientSecret,
            ClientType = ClientTypes.Confidential,
            DisplayName = string.IsNullOrWhiteSpace(client.DisplayName) ? client.ClientId.Trim() : client.DisplayName.Trim()
        };
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        descriptor.Properties[ProviderClientProperties.ProviderId] = JsonSerializer.SerializeToElement(client.ProviderId.ToString());
        descriptor.Properties[ProviderClientProperties.Role] = JsonSerializer.SerializeToElement(client.Role.Trim().ToLowerInvariant());
        descriptor.Properties[ProviderClientProperties.Contracts] = JsonSerializer.SerializeToElement(
            client.Contracts.Select(contract => contract.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase));

        await applications.CreateAsync(descriptor, cancellationToken);
        logger.LogInformation("Created provider integration client {ClientId} for provider {ProviderId}.",
            client.ClientId, client.ProviderId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void Validate(ProviderClientOptions client)
    {
        if (string.IsNullOrWhiteSpace(client.ClientId) || string.IsNullOrWhiteSpace(client.ClientSecret))
            throw new InvalidOperationException("BootstrapClient ClientId and ClientSecret are required when enabled.");
        if (client.ProviderId == Guid.Empty)
            throw new InvalidOperationException("BootstrapClient ProviderId is required when enabled.");
        if (client.Role is not (ProviderClaimTypes.Administrator or ProviderClaimTypes.Operator))
            throw new InvalidOperationException("BootstrapClient Role must be 'administrator' or 'operator'.");
        if (client.Contracts.Length == 0 || client.Contracts.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("BootstrapClient must contain at least one non-empty contract.");
    }
}
