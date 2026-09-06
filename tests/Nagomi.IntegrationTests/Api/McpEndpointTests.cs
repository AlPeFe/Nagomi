using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Nagomi.IntegrationTests.Api;

public sealed class McpEndpointTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Mcp_endpoint_requires_authentication()
    {
        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        var response = await anonymous.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Mcp_endpoint_allows_authenticated_handshake()
    {
        // A minimal MCP initialize over streamable HTTP. The exact framing is SDK-internal,
        // so we assert the endpoint is reachable and answers something non-404 for a user.
        var response = await _client.PostAsync("/mcp", new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""",
            System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
