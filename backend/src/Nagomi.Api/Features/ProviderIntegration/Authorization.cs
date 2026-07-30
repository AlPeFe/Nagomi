using System.Security.Claims;

namespace Nagomi.Api.Features.ProviderIntegration;

public static class ProviderClaimTypes
{
    public const string ProviderId = "nagomi_provider_id";
    public const string Contract = "nagomi_contract";
    public const string Role = "nagomi_integration_role";
    public const string Administrator = "administrator";
    public const string Operator = "operator";
}

public static class ProviderAuthorizationPolicies
{
    public const string Administration = "ProviderIntegrationAdministration";
    public const string Operations = "ProviderIntegrationOperations";
}

public sealed record ProviderIdentity(Guid ProviderId, string ClientId, IReadOnlySet<string> Contracts);

public enum ProviderAuthorizationFailure
{
    None,
    Unauthenticated,
    InvalidIdentity,
    Forbidden
}

public sealed record ProviderAuthorizationResult(
    ProviderAuthorizationFailure Failure,
    ProviderIdentity? Identity = null)
{
    public bool Succeeded => Failure == ProviderAuthorizationFailure.None;
}

public interface IProviderAuthorizer
{
    ProviderAuthorizationResult Authorize(ClaimsPrincipal principal, Guid providerId, string contractCode);
}

public sealed class OpenIddictClaimsProviderAuthorizer : IProviderAuthorizer
{
    public ProviderAuthorizationResult Authorize(ClaimsPrincipal principal, Guid providerId, string contractCode)
    {
        if (principal.Identity?.IsAuthenticated is not true)
            return new(ProviderAuthorizationFailure.Unauthenticated);

        var providerValue = principal.FindFirstValue(ProviderClaimTypes.ProviderId);
        var clientId = principal.FindFirstValue("client_id")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(providerValue, out var providerIdFromClaim) || string.IsNullOrWhiteSpace(clientId))
            return new(ProviderAuthorizationFailure.InvalidIdentity);

        var contracts = principal.FindAll(ProviderClaimTypes.Contract)
            .SelectMany(x => x.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (providerIdFromClaim != providerId || !contracts.Contains(contractCode))
            return new(ProviderAuthorizationFailure.Forbidden);

        return new(ProviderAuthorizationFailure.None, new ProviderIdentity(providerIdFromClaim, clientId, contracts));
    }
}
