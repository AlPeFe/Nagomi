using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.IntegrationTests.Api;

public sealed class DiagnosticAuthTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    [Fact]
    public async Task Me_without_token_authenticates_as_test_user()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("test-user");
        body.Should().Contain("admin");
    }

    [Fact]
    public async Task Me_with_role_header_authenticates_as_test_user()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, "default");
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("default");
    }

    [Fact]
    public async Task Web_endpoint_allows_test_user()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/operations/journeys");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonymous_header_returns_unauthorized()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AnonymousHeader, "true");
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_endpoint_rejects_default_role()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, "default");
        var response = await client.GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_endpoint_does_not_reject_admin_role()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/admin/users");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }
}
