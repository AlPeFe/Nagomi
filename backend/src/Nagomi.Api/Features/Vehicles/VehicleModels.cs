namespace Nagomi.Api.Features.Vehicles;

/// <summary>
/// A vehicle owned by a transport provider (the company that executes with its own resources).
/// World 1 (requester/clinic) has no vehicle management — it only sees the vehicle identity a
/// provider reports through integration (see ExternalCode matching ExternalResourceCode on status
/// events). World 2 (company with own fleet) manages real vehicles and assigns them to journeys.
/// </summary>
public sealed class TransportVehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>The provider (company) that owns this vehicle. Scopes all vehicle access.</summary>
    public Guid ProviderId { get; set; }
    /// <summary>Human-readable sequential public id, e.g. VHC-2026-000001.</summary>
    public string PublicId { get; set; } = null!;
    /// <summary>Display name / plate / internal code the coordinator recognizes the vehicle by.</summary>
    public string Name { get; set; } = null!;
    /// <summary>
    /// External code the company reports through integration (the ExternalResourceCode sent when a
    /// provider marks journey statuses). Lets the requester (world 1) attribute status points to a
    /// named vehicle without exposing fleet management.
    /// </summary>
    public string? ExternalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record VehicleResponse(
    Guid Id,
    string PublicId,
    string Name,
    string? ExternalCode,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record UpsertVehicleCommand(string Name, string? ExternalCode, string? Code = null, bool IsActive = true);

internal static class VehicleMapping
{
    internal static VehicleResponse ToResponse(this TransportVehicle vehicle) =>
        new(vehicle.Id, vehicle.PublicId, vehicle.Name, vehicle.ExternalCode, vehicle.IsActive, vehicle.CreatedAt);

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
