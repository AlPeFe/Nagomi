using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Infrastructure.PublicIds;

namespace Nagomi.Api.Features.Mcp;

/// <summary>
/// Nagomi's domain tools exposed over MCP (streamable HTTP at /mcp). Read-only and deliberately
/// small: the assistant (and external MCP clients) can look up patients, requests, the active
/// coordination board and the fleet without a single write. Grows only when a customer asks.
/// Registered scoped so each request resolves its own ITransportDb.
/// </summary>
[McpServerToolType]
public sealed class NagomiMcpTools(ITransportDb db)
{
    [McpServerTool(Name = "buscar_pacientes", Title = "Busca pacientes en el directorio por nombre, apellidos o documento. Devuelve hasta 10 resultados activos.")]
    public async Task<IReadOnlyList<object>> BuscarPacientesAsync(string? texto = null)
    {
        var term = texto?.Trim() ?? "";
        var query = db.Patients.AsNoTracking().Where(x => x.IsActive);
        if (term.Length > 0)
        {
            var norm = term.ToUpperInvariant();
            query = query.Where(x =>
                (x.FirstName != null && x.FirstName.Contains(term)) ||
                (x.LastName != null && x.LastName.Contains(term)) ||
                (x.DocumentNumber != null && x.DocumentNumber.Contains(norm)));
        }

        return await query
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Take(10)
            .Select(x => (object)new
            {
                id = x.Id,
                publicId = x.PublicId,
                nombre = ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(),
                documento = x.DocumentNumber,
                telefono = x.Phone
            })
            .ToListAsync();
    }

    [McpServerTool(Name = "consultar_paciente", Title = "Consulta el detalle de un paciente por su id (GUID) o publicId (PAT-...). Incluye tarjeta sanitaria y notas.")]
    public async Task<object?> ConsultarPacienteAsync(string id)
    {
        var patient = await db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id.ToString() == id || x.PublicId == id);
        if (patient is null) return null;
        return new
        {
            id = patient.Id,
            publicId = patient.PublicId,
            nombre = ((patient.FirstName ?? "") + " " + (patient.LastName ?? "")).Trim(),
            documento = patient.DocumentNumber,
            tarjetaSanitaria = patient.HealthCardNumber,
            telefono = patient.Phone,
            notas = patient.Notes,
            activo = patient.IsActive
        };
    }

    [McpServerTool(Name = "buscar_solicitudes", Title = "Busca solicitudes de transporte por texto (paciente, publicId REQ-/JRN-) y opcionalmente por estado (Draft, Active, Completed, Cancelled). Devuelve hasta 10.")]
    public async Task<IReadOnlyList<object>> BuscarSolicitudesAsync(string? texto = null, string? estado = null)
    {
        var query = db.TransportRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<TransportRequestStatus>(estado, ignoreCase: true, out var status))
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var term = texto.Trim();
            query = query.Where(x =>
                (x.PublicId != null && x.PublicId.Contains(term)) ||
                (x.Patient != null && (((x.Patient.FirstName ?? "") + " " + (x.Patient.LastName ?? "")).Contains(term) ||
                 (x.Patient.DocumentNumber != null && x.Patient.DocumentNumber.Contains(term)) ||
                 (x.Patient.Phone != null && x.Patient.Phone.Contains(term)))));
        }

        return await query
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .Select(x => (object)new
            {
                id = x.Id,
                publicId = x.PublicId,
                estado = x.Status.ToString(),
                paciente = x.Patient == null ? "" : ((x.Patient.FirstName ?? "") + " " + (x.Patient.LastName ?? "")).Trim(),
                motivo = x.Reason != null ? x.Reason.Description : null,
                origen = x.DefaultOrigin != null ? (x.DefaultOrigin.Name ?? x.DefaultOrigin.Street) : null,
                destino = x.DefaultDestination != null ? (x.DefaultDestination.Name ?? x.DefaultDestination.Street) : null,
                actualizado = x.UpdatedAt
            })
            .ToListAsync();
    }

    [McpServerTool(Name = "consultar_solicitud", Title = "Consulta una solicitud por id (GUID) o publicId (REQ-...). Devuelve el detalle completo con sus trayectos (ida/vuelta, horarios, estado, vehículo y conductor asignados).")]
    public async Task<object?> ConsultarSolicitudAsync(string id)
    {
        var request = await db.TransportRequests.AsNoTracking()
            .Include(x => x.JourneyRecords)
                .ThenInclude(j => j.Vehicle)
            .FirstOrDefaultAsync(x => x.Id.ToString() == id || x.PublicId == id);
        if (request is null) return null;

        return new
        {
            id = request.Id,
            publicId = request.PublicId,
            estado = request.Status.ToString(),
            paciente = request.Patient == null ? null : new
            {
                nombre = ((request.Patient.FirstName ?? "") + " " + (request.Patient.LastName ?? "")).Trim(),
                documento = request.Patient.DocumentNumber,
                telefono = request.Patient.Phone
            },
            motivo = request.Reason != null ? request.Reason.Description : null,
            origen = request.DefaultOrigin != null ? (request.DefaultOrigin.Name ?? request.DefaultOrigin.Street) : null,
            destino = request.DefaultDestination != null ? (request.DefaultDestination.Name ?? request.DefaultDestination.Street) : null,
            requisitos = request.Requirements != null ? request.Requirements.Mobility.ToString() : null,
            contrato = request.ContractCode,
            cliente = request.ClientName,
            notasPrivadas = request.PrivateNotes,
            trayectos = request.JourneyRecords.Select(j => new
            {
                id = j.Id,
                publicId = j.PublicId,
                direccion = j.Direction.ToString(),
                fechaServicio = j.ServiceDate,
                estado = j.CurrentStatus.ToString(),
                origen = j.Origin.Name ?? j.Origin.Street,
                destino = j.Destination.Name ?? j.Destination.Street,
                cita = j.Schedule.AppointmentAt,
                inicioPrevisto = j.Schedule.ScheduledStartAt,
                recogidaVuelta = j.Schedule.ScheduledPickupAt,
                vehiculo = j.VehicleId.HasValue ? (j.Vehicle != null ? j.Vehicle.PublicId : null) : null,
                conductor = j.DriverName,
                observaciones = j.ProviderVisibleNotes
            }).ToArray()
        };
    }

    [McpServerTool(Name = "listar_coordinacion", Title = "Lista el panel de coordinación: trayectos activos de hoy/mañana con su estado, vehículo asignado, conductor y última posición conocida. Es la vista de flota en movimiento.")]
    public async Task<IReadOnlyList<object>> ListarCoordinacionAsync()
    {
        var rows = await db.Journeys.AsNoTracking()
            .Include(x => x.StatusHistory)
            .Where(x => x.CurrentStatus != JourneyStatus.Completed && x.CurrentStatus != JourneyStatus.Cancelled)
            .OrderBy(x => x.ServiceDate)
            .Take(30)
            .ToListAsync();

        return rows.Select(j =>
        {
            var last = j.StatusHistory.OrderByDescending(s => s.OccurredAt).FirstOrDefault();
            var request = db.TransportRequests.AsNoTracking().FirstOrDefault(r => r.Id == j.TransportRequestId);
            return (object)new
            {
                trayecto = j.PublicId,
                direccion = j.Direction.ToString(),
                fecha = j.ServiceDate,
                estado = j.CurrentStatus.ToString(),
                paciente = request?.Patient == null ? "" : ((request.Patient.FirstName ?? "") + " " + (request.Patient.LastName ?? "")).Trim(),
                origen = j.Origin.Name ?? j.Origin.Street,
                destino = j.Destination.Name ?? j.Destination.Street,
                vehiculo = j.VehicleId.HasValue ? j.Vehicle?.PublicId : null,
                conductor = j.DriverName,
                ultimaPosicion = last is { Latitude: not null, Longitude: not null }
                    ? new { latitud = last.Latitude, longitud = last.Longitude, cuando = last.OccurredAt, estadoEvento = last.Status.ToString() }
                    : null
            };
        }).ToArray();
    }

    [McpServerTool(Name = "buscar_vehiculos", Title = "Lista la flota de vehículos con su tipo sanitario (Convencional, SVA, Pediátrica, Colectiva), código interno y externo.")]
    public async Task<IReadOnlyList<object>> BuscarVehiculosAsync()
    {
        return await db.Vehicles.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => (object)new
            {
                id = x.Id,
                publicId = x.PublicId,
                nombre = x.Name,
                tipo = x.VehicleType.ToString(),
                codigoExterno = x.ExternalCode
            })
            .ToListAsync();
    }
}
