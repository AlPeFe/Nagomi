using System.Security.Claims;
using FluentAssertions;
using Nagomi.Api.Features.ProviderIntegration;

namespace Nagomi.UnitTests.ProviderIntegration;

public sealed class AuthorizationTests
{
    private readonly OpenIddictClaimsProviderAuthorizer _authorizer = new();

    [Fact]
    public void Authorize_AcceptsMatchingProviderAndContractClaims()
    {
        var providerId = Guid.NewGuid();
        var principal = Principal(providerId, "CONTRACT-A");

        var result = _authorizer.Authorize(principal, providerId, "contract-a");

        result.Succeeded.Should().BeTrue();
        result.Identity!.ClientId.Should().Be("provider-client");
    }

    [Fact]
    public void Authorize_RejectsMatchingContractOwnedByAnotherProvider()
    {
        var principal = Principal(Guid.NewGuid(), "CONTRACT-A");

        var result = _authorizer.Authorize(principal, Guid.NewGuid(), "CONTRACT-A");

        result.Failure.Should().Be(ProviderAuthorizationFailure.Forbidden);
    }

    [Fact]
    public void Authorize_RejectsContractOutsideTokenClaims()
    {
        var providerId = Guid.NewGuid();
        var result = _authorizer.Authorize(Principal(providerId, "CONTRACT-A"), providerId, "CONTRACT-B");
        result.Failure.Should().Be(ProviderAuthorizationFailure.Forbidden);
    }

    private static ClaimsPrincipal Principal(Guid providerId, string contract) => new(
        new ClaimsIdentity(
        [
            new Claim(ProviderClaimTypes.ProviderId, providerId.ToString()),
            new Claim(ProviderClaimTypes.Contract, contract),
            new Claim("client_id", "provider-client")
        ], "OpenIddict.Validation.AspNetCore"));
}
