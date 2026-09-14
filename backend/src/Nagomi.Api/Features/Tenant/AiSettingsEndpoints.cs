using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagomi.Api.Features.HelpChat;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.Tenant;

/// <summary>
/// Configuración del asistente de IA (chat de ayuda), editable desde la web: URL del
/// gateway de Hermes (o cualquier proveedor OpenAI-compatible), modelo y credenciales.
/// El secreto se guarda en el servidor y <b>nunca</b> se devuelve al navegador.
/// </summary>
public sealed record AiSettingsResponse(
    bool Enabled, string? Provider, string? BaseUrl, string? Model, string? Username,
    bool HasPassword, string? SystemPrompt, bool EnableTools);

/// <summary>Actualización de la configuración. Contraseña vacía = mantener la guardada.</summary>
public sealed record AiSettingsCommand(
    bool Enabled, string? Provider, string? BaseUrl, string? Model, string? Username,
    string? Password, bool ClearPassword = false, string? SystemPrompt = null, bool EnableTools = true);

/// <summary>Prueba de conexión. Cada campo es opcional: si falta, se usa lo guardado.</summary>
public sealed record AiConnectionTestCommand(
    string? BaseUrl = null, string? Model = null, string? Username = null,
    string? Password = null, string? ApiKey = null, string? Provider = null);

public sealed record AiConnectionTestResponse(bool Ok, int Status, string Detail);

public static class AiSettingsEndpoints
{
    public static IEndpointRouteBuilder MapAiSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/tenant/ai")
            .WithTags("Tenant")
            .RequireAuthorization(UserAuthorizationPolicies.Admin);

        admin.MapGet("", Get);
        admin.MapPut("", Update);
        admin.MapPost("/test", Test);

        return endpoints;
    }

    private static async Task<Ok<AiSettingsResponse>> Get(ITenantDb db, CancellationToken cancellationToken)
    {
        var settings = await TenantSettingsEndpoints.EnsureSettingsAsync(db, cancellationToken);
        return TypedResults.Ok(ToResponse(settings));
    }

    private static async Task<Results<Ok<AiSettingsResponse>, ValidationProblem>> Update(
        AiSettingsCommand command, ITenantDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (command is null) return TypedResults.ValidationProblem(Error("request", "Falta la configuración."));
        var settings = await TenantSettingsEndpoints.EnsureSettingsAsync(db, cancellationToken);

        settings.AiEnabled = command.Enabled;
        settings.AiProvider = Clean(command.Provider);
        settings.AiBaseUrl = Clean(command.BaseUrl);
        settings.AiModel = Clean(command.Model);
        settings.AiUsername = Clean(command.Username);
        settings.AiSystemPrompt = Clean(command.SystemPrompt);
        settings.AiEnableTools = command.EnableTools;

        // El secreto sólo cambia si llega uno nuevo (o si se pide borrarlo): así el
        // formulario puede mostrar "dejar como está" sin exponer la contraseña.
        if (command.ClearPassword) settings.AiPassword = null;
        else if (!string.IsNullOrWhiteSpace(command.Password)) settings.AiPassword = command.Password.Trim();

        settings.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(settings));
    }

    /// <summary>Prueba la conexión sin necesidad de guardar: pide una respuesta mínima al modelo.</summary>
    private static async Task<Ok<AiConnectionTestResponse>> Test(
        AiConnectionTestCommand command, ITenantDb db, IOptions<HelpChatOptions> fallback,
        IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var stored = await TenantSettingsEndpoints.EnsureSettingsAsync(db, cancellationToken);
        var baseUrl = Clean(command.BaseUrl) ?? stored.AiBaseUrl ?? fallback.Value.BaseUrl;
        var model = Clean(command.Model) ?? stored.AiModel ?? fallback.Value.Model;
        var username = Clean(command.Username) ?? stored.AiUsername;
        var provider = Clean(command.Provider) ?? stored.AiProvider;
        // Una contraseña nueva en el formulario tiene prioridad sobre la guardada.
        var password = string.IsNullOrWhiteSpace(command.Password) ? stored.AiPassword : command.Password;
        var apiKey = Clean(command.ApiKey) ?? (provider == "hermes" ? null : fallback.Value.ApiKey);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
            return TypedResults.Ok(new AiConnectionTestResponse(false, 0, "Faltan la URL del gateway o el modelo."));

        var endpoint = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "chat/completions");
        try
        {
            using var http = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    model,
                    messages = new object[] { new { role = "user", content = "ping" } },
                    max_tokens = 8,
                }),
            };
            ApplyAuthentication(http, username, password, apiKey);

            using var client = httpClientFactory.CreateClient(HelpChatEndpoints.HttpClientName);
            using var response = await client.SendAsync(http, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType;

            // Un 200 NO basta: hay que comprobar que la respuesta es realmente una
            // completion de chat. Si no, el asistente fallará luego y el usuario no
            // sabría por qué (un panel web devuelve 200 con HTML, por ejemplo).
            var looksLikeChat = false;
            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);
                    looksLikeChat = document.RootElement.TryGetProperty("choices", out var choices)
                        && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0;
                }
                catch (JsonException) { looksLikeChat = false; }
            }

            var status = (int)response.StatusCode;
            string detail;
            if (looksLikeChat)
                detail = $"Conexión correcta con {endpoint.Host} ({model}).";
            else if (status is 401 or 403)
                detail = $"El gateway responde pero rechaza las credenciales ({status}). Revisa usuario y contraseña.";
            else if (!response.IsSuccessStatusCode)
                detail = $"El gateway respondió {status}: {Truncate(body)}";
            else if (body.TrimStart().StartsWith('<'))
                detail = "La URL responde una página web, no el API de chat. Si es el panel del gateway de Hermes, "
                    + "aquí se autentica con sesión de navegador (cookie): apunta a un endpoint compatible con OpenAI.";
            else
                detail = $"Respondió {status} ({mediaType ?? "sin tipo"}) pero sin 'choices': no parece una respuesta de chat.";

            return TypedResults.Ok(new AiConnectionTestResponse(looksLikeChat, status, detail));
        }
        catch (Exception exception)
        {
            return TypedResults.Ok(new AiConnectionTestResponse(false, 0, $"No se pudo contactar: {exception.Message}"));
        }
    }

    /// <summary>Basic (usuario+contraseña, como el gateway de Hermes) o Bearer (clave de API).</summary>
    internal static void ApplyAuthentication(HttpRequestMessage http, string? username, string? password, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            http.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }
        else if (!string.IsNullOrWhiteSpace(apiKey))
        {
            http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    internal static AiSettingsResponse ToResponse(TenantSettings settings) => new(
        settings.AiEnabled, settings.AiProvider, settings.AiBaseUrl, settings.AiModel, settings.AiUsername,
        !string.IsNullOrWhiteSpace(settings.AiPassword), settings.AiSystemPrompt, settings.AiEnableTools);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";

    private static Dictionary<string, string[]> Error(string key, string message) => new() { [key] = [message] };
}
