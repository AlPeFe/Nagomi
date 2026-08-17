using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.HelpChat;

public static class HelpChatEndpoints
{
    public const string HttpClientName = "Nagomi.HelpChat";

    public static IEndpointRouteBuilder MapHelpChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/help-chat")
            .WithTags("HelpChat")
            .RequireAuthorization(UserAuthorizationPolicies.Web);

        group.MapGet("/status", GetStatus);
        group.MapPost("/messages", SendMessage);

        return endpoints;
    }

    /// <summary>Reports whether the help chat is enabled, so the frontend can show/hide its button.</summary>
    private static Ok<HelpChatStatusResponse> GetStatus(
        IOptions<HelpChatOptions> options)
    {
        var configured = options.Value.IsConfigured;
        return TypedResults.Ok(new HelpChatStatusResponse(configured));
    }

    /// <summary>Forwards a user message to the configured OpenAI-compatible chat completions endpoint.</summary>
    private static async Task<Results<Ok<HelpChatMessageResponse>, ValidationProblem, StatusCodeHttpResult>> SendMessage(
        HelpChatMessageRequest request,
        IOptions<HelpChatOptions> options,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Nagomi.HelpChat");
        var opts = options.Value;
        if (!opts.IsConfigured)
            return TypedResults.ValidationProblem(Error("help-chat", "El chat de ayuda no está configurado."));

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
            return TypedResults.ValidationProblem(Error("message", "El mensaje no puede estar vacío."));

        var history = request.History
            .TakeLast(20)
            .Where(h => !string.IsNullOrWhiteSpace(h.Content) && h.Role is "user" or "assistant" or "system");

        var messages = new List<object> { new { role = "system", content = opts.SystemPrompt ?? DefaultSystemPrompt } };
        messages.AddRange(history.Select(h => new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = request.Message.Trim() });

        using var client = httpClientFactory.CreateClient(HttpClientName);
        var chatEndpoint = new Uri(new Uri(opts.BaseUrl!.TrimEnd('/') + "/"), "chat/completions");
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(chatEndpoint, new
            {
                model = opts.Model,
                messages,
                temperature = 0.3,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Help chat: no se pudo contactar con el proveedor {BaseUrl}", opts.BaseUrl);
            return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Help chat: el proveedor respondió {Status}: {Body}", (int)response.StatusCode, body);
            return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
        }

        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var reply = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (string.IsNullOrWhiteSpace(reply))
                return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
            return TypedResults.Ok(new HelpChatMessageResponse(reply));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Help chat: respuesta del proveedor con formato inesperado");
            return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    private const string DefaultSystemPrompt =
        "Eres el asistente de ayuda de Nagomi, un sistema de coordinación de transporte de pacientes. " +
        "Responde de forma breve, clara y en español. Si no sabes algo, dilo.";

    private static Dictionary<string, string[]> Error(string key, string message) => new() { [key] = [message] };
}
