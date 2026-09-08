using Nagomi.Api.Domain;

namespace Nagomi.Api.Features.Routes;

/// <summary>
/// State of a collective route: the coordinator groups several same-day journeys into one
/// vehicle run (the "colectivo"). Individual journeys simply don't belong to any route, so a
/// vehicle can run a collective route in the morning and a plain individual journey in the
/// afternoon — the two coexist. A route NEVER changes the journey model or its statuses; it is
/// purely an organizational layer above existing journeys.
/// </summary>
public enum RouteStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public sealed class CollectiveRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Human-readable sequential public id, e.g. RUT-2026-000001.</summary>
    public string PublicId { get; set; } = null!;
    /// <summary>All stops belong to the same service day.</summary>
    public DateOnly ServiceDate { get; set; }
    public Guid? VehicleId { get; set; }
    public string? DriverName { get; set; }
    public string? Notes { get; set; }
    public RouteStatus Status { get; set; } = RouteStatus.Planned;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<CollectiveRouteStop> Stops { get; set; } = [];
}

public sealed class CollectiveRouteStop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RouteId { get; set; }
    /// <summary>The existing journey this stop groups (each patient keeps their own request/journey).</summary>
    public Guid JourneyId { get; set; }
    /// <summary>Order within the route (1, 2, 3…).</summary>
    public int Order { get; set; }
}
