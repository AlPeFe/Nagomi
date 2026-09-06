using System.Text.Json;
using Nagomi.Api.Features.Mcp;

namespace Nagomi.Api.Features.HelpChat;

/// <summary>
/// Bridges the help chat to the same domain tools exposed over MCP. The assistant can call
/// buscar_pacientes / listar_coordinacion / ... and answer with real Nagomi data. Deliberately
/// read-only; grows only when a customer asks.
/// </summary>
internal static class HelpChatTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>OpenAI-compatible tool schemas sent in the chat/completions request.</summary>
    public static IReadOnlyList<object> Schemas { get; } = new object[]
    {
        new
        {
            type = "function",
            function = new
            {
                name = "buscar_pacientes",
                description = "Busca pacientes en el directorio por nombre, apellidos o documento. Devuelve hasta 10 resultados activos.",
                parameters = new
                {
                    type = "object",
                    properties = new { texto = new { type = "string", description = "Nombre, apellidos o documento del paciente." } },
                    additionalProperties = false
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "consultar_paciente",
                description = "Consulta el detalle de un paciente por su id (GUID) o publicId (PAT-...).",
                parameters = new
                {
                    type = "object",
                    properties = new { id = new { type = "string", description = "Id (GUID) o publicId (PAT-...) del paciente." } },
                    required = new[] { "id" },
                    additionalProperties = false
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "buscar_solicitudes",
                description = "Busca solicitudes de transporte por texto (paciente, publicId REQ-/JRN-) y opcionalmente por estado.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        texto = new { type = "string", description = "Texto a buscar: paciente, publicId, teléfono." },
                        estado = new { type = "string", @enum = new[] { "Draft", "Active", "Completed", "Cancelled" }, description = "Filtro por estado (opcional)." }
                    },
                    additionalProperties = false
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "consultar_solicitud",
                description = "Consulta una solicitud por id (GUID) o publicId (REQ-...). Devuelve el detalle completo con sus trayectos.",
                parameters = new
                {
                    type = "object",
                    properties = new { id = new { type = "string", description = "Id (GUID) o publicId (REQ-...) de la solicitud." } },
                    required = new[] { "id" },
                    additionalProperties = false
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "listar_coordinacion",
                description = "Lista el panel de coordinación: trayectos activos de hoy/mañana con estado, vehículo, conductor y última posición.",
                parameters = new { type = "object", properties = new { }, additionalProperties = false }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "buscar_vehiculos",
                description = "Lista la flota de vehículos con su tipo sanitario (Convencional, SVA, Pediátrica, Colectiva), código interno y externo.",
                parameters = new { type = "object", properties = new { }, additionalProperties = false }
            }
        }
    };

    /// <summary>Executes a tool call by name and returns its result serialized as JSON text.</summary>
    public static async Task<string> ExecuteAsync(NagomiMcpTools tools, string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        var args = arguments.ValueKind == JsonValueKind.Object ? arguments : default;
        object? result = name switch
        {
            "buscar_pacientes" => await tools.BuscarPacientesAsync(GetString(args, "texto")),
            "consultar_paciente" => await tools.ConsultarPacienteAsync(GetString(args, "id") ?? ""),
            "buscar_solicitudes" => await tools.BuscarSolicitudesAsync(GetString(args, "texto"), GetString(args, "estado")),
            "consultar_solicitud" => await tools.ConsultarSolicitudAsync(GetString(args, "id") ?? ""),
            "listar_coordinacion" => await tools.ListarCoordinacionAsync(),
            "buscar_vehiculos" => await tools.BuscarVehiculosAsync(),
            _ => throw new InvalidOperationException($"Herramienta desconocida: {name}")
        };
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private static string? GetString(JsonElement args, string property)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        return args.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
