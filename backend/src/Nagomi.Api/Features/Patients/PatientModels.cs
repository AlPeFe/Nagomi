namespace Nagomi.Api.Features.Patients;

/// <summary>
/// A patient master record in the small company's directory. Requests keep their own
/// PatientDetails snapshot (per spec, requests for the same patient remain independent);
/// this directory is what lets the coordinator find a known patient and prefill a request,
/// and lets the assistant (MCP) look patients up without re-typing their data.
/// </summary>
public sealed class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Human-readable sequential public id, e.g. PAT-2026-000001.</summary>
    public string PublicId { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    /// <summary>DNI/NIE/passport. Normalized (trimmed) and unique when present.</summary>
    public string? DocumentNumber { get; set; }
    /// <summary>Health card number (CIP/TSI). Not unique — can be unknown or repeated.</summary>
    public string? HealthCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record PatientResponse(
    Guid Id,
    string PublicId,
    string? FirstName,
    string? LastName,
    string? DocumentNumber,
    string? HealthCardNumber,
    string? Phone,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Create/update payload. A patient needs at least a name or a document number.</summary>
public sealed record UpsertPatientCommand(
    string? FirstName,
    string? LastName,
    string? DocumentNumber = null,
    string? HealthCardNumber = null,
    string? Phone = null,
    string? Notes = null);

internal static class PatientMapping
{
    internal static PatientResponse ToResponse(this Patient patient) =>
        new(patient.Id, patient.PublicId, patient.FirstName, patient.LastName,
            patient.DocumentNumber, patient.HealthCardNumber, patient.Phone,
            patient.Notes, patient.IsActive, patient.CreatedAt);

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
