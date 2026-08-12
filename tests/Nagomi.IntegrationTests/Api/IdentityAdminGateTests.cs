using System.Net;
using FluentAssertions;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.IntegrationTests.Api;

public sealed class IdentityAdminGateTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    [Fact]
    public async Task Identity_endpoint_rejects_default_role()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, "default");
        var response = await client.GetAsync("/api/admin/identity/clients");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Identity_endpoint_requires_authentication()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AnonymousHeader, "true");
        var response = await client.GetAsync("/api/admin/identity/clients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Identity_endpoint_is_not_blocked_for_admin_role()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/admin/identity/clients");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }
}
