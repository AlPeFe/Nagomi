using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.Dispatch;

/// <summary>
/// Real-time dispatch notifications over SignalR. A driver that logs into a vehicle subscribes to
/// the group for its vehicle code (the code is a claim on the driver token). When a journey or a
/// collective route is assigned to that vehicle, the backend pushes a lightweight "new work for
/// you" event to the group — the Android app no longer needs to poll for new assignments.
/// </summary>
[Authorize]
public sealed class DispatchHub : Hub
{
    public const string Route = "/hubs/dispatch";
    public const string VehicleCodeClaim = "vehicle_code";

    /// <summary>The driver tells the server "I am vehicle X" and joins its notification group.</summary>
    public Task SubscribeVehicle(string vehicleCode)
    {
        var normalized = vehicleCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new HubException("El código de vehículo no puede estar vacío.");
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(normalized));
    }

    public Task UnsubscribeVehicle(string vehicleCode)
    {
        var normalized = vehicleCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return Task.CompletedTask;
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(normalized));
    }

    public static string GroupName(string vehicleCode) => $"vehicle:{vehicleCode.Trim().ToUpperInvariant()}";
}

/// <summary>Payload pushed when work is assigned to a vehicle.</summary>
public sealed record DispatchNotification(
    string Kind,        // "journey" | "route"
    string PublicId,    // JRN-... or RUT-...
    Guid EntityId,
    string VehicleCode,
    DateTimeOffset AssignedAt);

public static class DispatchEndpoints
{
    public static IEndpointRouteBuilder MapDispatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<DispatchHub>(DispatchHub.Route);
        return endpoints;
    }
}
