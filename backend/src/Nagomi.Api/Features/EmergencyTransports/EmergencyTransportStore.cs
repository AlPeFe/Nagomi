using Nagomi.Api.Domain;

namespace Nagomi.Api.Features.EmergencyTransports;

public enum EmergencyTransportStatus
{
    Active,
    Completed,
    Cancelled
}

public sealed class IncidentLocation
{
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string? Address { get; private set; }
    public string? Municipality { get; private set; }
    public string? Notes { get; private set; }

    private IncidentLocation()
    {
    }

    public IncidentLocation(
        decimal latitude,
        decimal longitude,
        string? address = null,
        string? municipality = null,
        string? notes = null)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainValidationException("Incident coordinates are outside their valid ranges.");
        }

        Latitude = latitude;
        Longitude = longitude;
        Address = Clean(address);
        Municipality = Clean(municipality);
        Notes = Clean(notes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class EmergencyTransportRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicId { get; set; } = null!;
    public EmergencyTransportStatus Status { get; set; } = EmergencyTransportStatus.Active;
    public string Reason { get; set; } = null!;
    public string? ContactPhone { get; set; }
    public IncidentLocation Incident { get; set; } = null!;
    public string? Observations { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static EmergencyTransportRecord Create(
        string reason,
        IncidentLocation incident,
        string? contactPhone,
        string? observations,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("A reason is required.");
        }

        return new EmergencyTransportRecord
        {
            PublicId = $"EMG-{Guid.NewGuid():N}".ToUpperInvariant(),
            Reason = reason.Trim(),
            Incident = incident,
            ContactPhone = Clean(contactPhone),
            Observations = Clean(observations),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
