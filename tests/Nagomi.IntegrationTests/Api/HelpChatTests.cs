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
    public async Task Chat_sends_domain_tools_to_the_model()
    {
        var handler = new CapturingHandler();
        var client = ConfiguredClient(handler);

        await client.PostAsJsonAsync("/api/help-chat/messages", new HelpChatMessageRequest("¿qué vehículos hay?", null));

        handler.Bodies.Should().NotBeEmpty();
        var first = handler.Bodies[0];
        first.Should().Contain("\"tools\"");
        first.Should().Contain("buscar_vehiculos");
        first.Should().Contain("listar_coordinacion");
    }

    [Fact]
    public async Task Chat_executes_tool_call_and_feeds_result_back()
    {
        var handler = new ToolCallHandler();
        var client = ConfiguredClient(handler);

        var response = await client.PostAsJsonAsync("/api/help-chat/messages",
            new HelpChatMessageRequest("¿qué vehículos hay?", null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reply = await response.Content.ReadFromJsonAsync<HelpChatMessageResponse>();
        reply!.Reply.Should().Be("Respuesta final con datos");

        handler.Bodies.Should().HaveCount(2);
        // Second round carries the tool result back to the model.
        var second = handler.Bodies[1];
        second.Should().Contain("\"role\":\"tool\"");
        second.Should().Contain("\"tool_call_id\":\"call_1\"");
        second.Should().Contain("buscar_vehiculos");
    }

    [Fact]
    public async Task Chat_degrades_when_provider_rejects_tools()
    {
        var handler = new RejectToolsThenReplyHandler();
        var client = ConfiguredClient(handler);

        var response = await client.PostAsJsonAsync("/api/help-chat/messages",
            new HelpChatMessageRequest("hola", null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reply = await response.Content.ReadFromJsonAsync<HelpChatMessageResponse>();
        reply!.Reply.Should().Be("Sin herramientas");

        handler.Bodies.Should().HaveCount(2);
        handler.Bodies[0].Should().Contain("\"tools\"");
        handler.Bodies[1].Should().NotContain("\"tools\"");
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

    /// <summary>Builds a client with a custom fake handler (for tool-call scenarios).</summary>
    private HttpClient ConfiguredClient(HttpMessageHandler handler)
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
                services.AddSingleton<IHttpClientFactory>(new HandlerClientFactory(handler));
            });
        });
        return configured.CreateClient();
    }

    private sealed class HandlerClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    /// <summary>Captures request bodies and always replies with a plain content message.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"OK"}}]}""",
                    Encoding.UTF8, "application/json"),
            };
        }
    }

    /// <summary>First call replies with a tool_call (buscar_vehiculos), second with final content.</summary>
    private sealed class ToolCallHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        private int _calls;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            var response = _calls++ == 0
                ? """{"choices":[{"message":{"content":null,"tool_calls":[{"id":"call_1","type":"function","function":{"name":"buscar_vehiculos","arguments":"{}"}}]}}]}"""
                : """{"choices":[{"message":{"content":"Respuesta final con datos"}}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }

    /// <summary>Rejects the first (tools) request with 400, then replies without tools.</summary>
    private sealed class RejectToolsThenReplyHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        private int _calls;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            if (_calls++ == 0)
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"tools not supported\"}", Encoding.UTF8, "application/json"),
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"Sin herramientas"}}]}""",
                    Encoding.UTF8, "application/json"),
            };
        }
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
