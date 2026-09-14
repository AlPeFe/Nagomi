using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagomi.Api.Features.Tenant;

namespace Nagomi.Api.Features.HelpChat;

/// <summary>
/// Configuración EFECTIVA del chat de ayuda: lo que hay en la base de datos (editable
/// desde la web, campo a campo) y, si la instalación aún no tiene nada configurado,
/// lo que venga de appsettings/.env. Así una instalación existente sigue arrancando
/// igual y una nueva se configura desde la UI sin tocar ficheros.
/// </summary>
public sealed class EffectiveHelpChatSettings
{
    public bool Enabled { get; init; }
    public string? Provider { get; init; }
    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? SystemPrompt { get; init; }
    public bool EnableTools { get; init; } = true;

    /// <summary>
    /// Basta con URL y modelo. A diferencia de la comprobación antigua, NO se exige una
    /// clave de API: el gateway de Hermes autentica con usuario y contraseña, y un Ollama
    /// local no necesita credencial alguna.
    /// </summary>
    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);
}

public interface IHelpChatSettingsProvider
{
    Task<EffectiveHelpChatSettings> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class HelpChatSettingsProvider(ITenantDb db, IOptions<HelpChatOptions> fallback)
    : IHelpChatSettingsProvider
{
    public async Task<EffectiveHelpChatSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var stored = await db.TenantSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == TenantSettings.SingletonId, cancellationToken);

        var legacy = fallback.Value;
        // Si la instalación todavía no tiene nada configurado por la web, se respeta
        // appsettings/.env para no romper despliegues existentes.
        var hasStored = stored is not null &&
            (stored.AiEnabled || !string.IsNullOrWhiteSpace(stored.AiBaseUrl));
        if (!hasStored)
        {
            return new EffectiveHelpChatSettings
            {
                Enabled = legacy.Enabled,
                BaseUrl = legacy.BaseUrl,
                Model = legacy.Model,
                ApiKey = legacy.ApiKey,
                SystemPrompt = legacy.SystemPrompt,
                EnableTools = legacy.EnableTools,
            };
        }

        return new EffectiveHelpChatSettings
        {
            Enabled = stored!.AiEnabled,
            Provider = stored.AiProvider,
            BaseUrl = stored.AiBaseUrl,
            Model = stored.AiModel,
            Username = stored.AiUsername,
            Password = stored.AiPassword,
            SystemPrompt = stored.AiSystemPrompt,
            EnableTools = stored.AiEnableTools,
            // Un despliegue puede tener la clave del proveedor en el entorno aunque la
            // configuración venga de la web: se conserva como respaldo.
            ApiKey = string.IsNullOrWhiteSpace(stored.AiUsername) ? legacy.ApiKey : null,
        };
    }
}

/// <summary>Lo que la web necesita saber del asistente (sin secretos).</summary>
public sealed record HelpChatStatusResponse(bool Enabled, string? Provider, string? Model);
