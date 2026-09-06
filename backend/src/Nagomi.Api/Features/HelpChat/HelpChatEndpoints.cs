using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Nagomi.Api.Features.Mcp;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.HelpChat;

public static class HelpChatEndpoints
{
    public const string HttpClientName = "Nagomi.HelpChat";
    private const int MaxToolIterations = 4;

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

    /// <summary>
    /// Forwards a user message to the configured OpenAI-compatible chat completions endpoint.
    /// When the model asks for one of Nagomi's domain tools (function calling), the call is
    /// executed locally and the result is fed back, looping until a final answer is produced.
    /// </summary>
    private static async Task<Results<Ok<HelpChatMessageResponse>, ValidationProblem, StatusCodeHttpResult>> SendMessage(
        HelpChatMessageRequest request,
        IOptions<HelpChatOptions> options,
        IHttpClientFactory httpClientFactory,
        NagomiMcpTools domainTools,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Nagomi.HelpChat");
        var opts = options.Value;
        if (!opts.IsConfigured)
            return TypedResults.ValidationProblem(Error("help-chat", "El chat de ayuda no está configurado."));

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
            return TypedResults.ValidationProblem(Error("message", "El mensaje no puede estar vacío."));

        using var client = httpClientFactory.CreateClient(HttpClientName);
        var chatEndpoint = new Uri(new Uri(opts.BaseUrl!.TrimEnd('/') + "/"), "chat/completions");

        var history = request.History
            .TakeLast(20)
            .Where(h => !string.IsNullOrWhiteSpace(h.Content) && h.Role is "user" or "assistant" or "system");

        var messages = new List<object> { new { role = "system", content = opts.SystemPrompt ?? DefaultSystemPrompt } };
        messages.AddRange(history.Select(h => new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = request.Message.Trim() });

        var useTools = opts.EnableTools;
        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            HttpResponseMessage response;
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["model"] = opts.Model,
                    ["messages"] = messages,
                    ["temperature"] = 0.3,
                };
                if (useTools)
                    payload["tools"] = HelpChatTools.Schemas;
                response = await client.PostAsJsonAsync(chatEndpoint, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Help chat: no se pudo contactar con el proveedor {BaseUrl}", opts.BaseUrl);
                return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                // Some providers reject the tools field; degrade to a plain chat once.
                if (useTools && (int)response.StatusCode == 400)
                {
                    useTools = false;
                    continue;
                }
                logger.LogWarning("Help chat: el proveedor respondió {Status}: {Body}", (int)response.StatusCode, body);
                return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
            }

            JsonElement message;
            try
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                // Clone so the element outlives the document (using var disposes it at scope end).
                message = document.RootElement.GetProperty("choices")[0].GetProperty("message").Clone();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Help chat: respuesta del proveedor con formato inesperado");
                return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
            }

            var content = message.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString()
                : null;

            // Tool call requested? Execute locally and continue the loop.
            if (useTools && message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array && toolCalls.GetArrayLength() > 0)
            {
                messages.Add(new { role = "assistant", content = (string?)null, tool_calls = toolCalls });

                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var toolCallId = toolCall.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                    var function = toolCall.TryGetProperty("function", out var fn) ? fn : default;
                    var toolName = function.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    var arguments = function.TryGetProperty("arguments", out var argsElement) ? argsElement : default;

                    if (toolCallId is null || string.IsNullOrWhiteSpace(toolName))
                    {
                        messages.Add(new { role = "tool", tool_call_id = toolCallId ?? "", content = "Error: llamada a herramienta mal formada." });
                        continue;
                    }

                    JsonElement argsJson;
                    if (arguments.ValueKind == JsonValueKind.String)
                    {
                        try { argsJson = JsonDocument.Parse(arguments.GetString()!).RootElement.Clone(); }
                        catch { argsJson = default; }
                    }
                    else
                    {
                        argsJson = arguments.Clone();
                    }

                    string resultText;
                    try
                    {
                        resultText = await HelpChatTools.ExecuteAsync(domainTools, toolName, argsJson, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Help chat: error ejecutando herramienta {Tool}", toolName);
                        resultText = $"{{\"error\": \"{ex.Message}\"}}";
                    }

                    messages.Add(new { role = "tool", tool_call_id = toolCallId, content = resultText });
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
                return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
            return TypedResults.Ok(new HelpChatMessageResponse(content));
        }

        logger.LogWarning("Help chat: demasiadas iteraciones de herramientas sin respuesta final");
        return TypedResults.StatusCode(StatusCodes.Status502BadGateway);
    }

    private const string DefaultSystemPrompt =
        "Eres el asistente de ayuda de Nagomi, un sistema de coordinación de transporte de pacientes. " +
        "Responde de forma breve, clara y en español. Si el usuario pregunta por datos reales " +
        "(pacientes, solicitudes, coordinación, vehículos), usa las herramientas disponibles. " +
        "Si no sabes algo, dilo.";

    private static Dictionary<string, string[]> Error(string key, string message) => new() { [key] = [message] };
}
