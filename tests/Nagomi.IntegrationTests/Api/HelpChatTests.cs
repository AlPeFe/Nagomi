using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nagomi.Api.Features.HelpChat;

namespace Nagomi.IntegrationTests.Api;

public sealed class HelpChatTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Status_is_disabled_when_not_configured()
    {
        var response = await _client.GetFromJsonAsync<HelpChatStatusResponse>("/api/help-chat/status");
        response.Should().NotBeNull();
        response!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Sending_message_when_not_configured_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/help-chat/messages", new HelpChatMessageRequest("hola", null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Empty_message_is_rejected()
    {
        var configured = ConfiguredClient();
        var response = await configured.PostAsJsonAsync("/api/help-chat/messages", new HelpChatMessageRequest("   ", null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Help_chat_endpoints_require_authentication()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        var response = await client.GetAsync("/api/help-chat/status");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Enabled_chat_forwards_message_and_returns_reply()
    {
        var client = ConfiguredClient(fakeReply: "Respuesta de prueba");

        var response = await client.PostAsJsonAsync("/api/help-chat/messages", new HelpChatMessageRequest(
            "¿cómo creo un traslado?",
            new List<HelpChatHistoryItem> { new("user", "hola") }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reply = await response.Content.ReadFromJsonAsync<HelpChatMessageResponse>();
        reply!.Reply.Should().Be("Respuesta de prueba");
    }

    [Fact]
    public async Task Enabled_status_is_reported()
    {
        var client = ConfiguredClient();
        var status = await client.GetFromJsonAsync<HelpChatStatusResponse>("/api/help-chat/status");
        status.Should().NotBeNull();
        status!.Enabled.Should().BeTrue();
    }

    /// <summary>Builds a client against a factory with HelpChat configured and an in-memory fake HTTP provider.</summary>
    private HttpClient ConfiguredClient(string? fakeReply = null)
    {
        var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("HelpChat:Enabled", "true");
            builder.UseSetting("HelpChat:BaseUrl", "https://fake.local/v1");
            builder.UseSetting("HelpChat:ApiKey", "secret-test");
            builder.UseSetting("HelpChat:Model", "test-model");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(fakeReply ?? "OK"));
            });
        });
        return configured.CreateClient();
    }

    private sealed class FakeHttpClientFactory(string reply) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new FakeHandler(reply));
        }

        private sealed class FakeHandler(string reply) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = $"{{\"choices\":[{{\"message\":{{\"content\":{JsonQuote(reply)}}}}}]}}";
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(response);
            }

            private static string JsonQuote(string value) =>
                System.Text.Json.JsonSerializer.Serialize(value);
        }
    }
}
