using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.Tenant;

/// <summary>Flags describing which operating roles this Nagomi installation enables.</summary>
[Flags]
public enum TenantCapabilities
{
    None = 0,
    PublishesRequests = 1,
    ExecutesTransports = 2,
    HandlesEmergencies = 4
}

/// <summary>Global, single-row tenant operating configuration.</summary>
public sealed class TenantSettings
{
    public static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid Id { get; set; } = SingletonId;
    public TenantCapabilities Capabilities { get; set; } =
        TenantCapabilities.PublishesRequests | TenantCapabilities.ExecutesTransports | TenantCapabilities.HandlesEmergencies;
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Billable client: an organization or individual invoiced for a transport but not integrated into Nagomi.</summary>
public sealed class TransportClient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? TaxId { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public interface ITenantDb
{
    DbSet<TenantSettings> TenantSettings { get; }
    DbSet<TransportClient> TransportClients { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record TenantSettingsCommand(bool PublishesRequests, bool ExecutesTransports, bool HandlesEmergencies);
public sealed record TenantSettingsResponse(bool PublishesRequests, bool ExecutesTransports, bool HandlesEmergencies);

public sealed record UpsertClientCommand(string Name, string? TaxId, string? ContactPerson, string? Phone, string? Email, string? Address, bool IsActive = true);
public sealed record ClientResponse(
    Guid Id, string PublicId, string Name, string? TaxId, string? ContactPerson,
    string? Phone, string? Email, string? Address, bool IsActive, DateTimeOffset CreatedAt);

internal static class ClientMapping
{
    internal static ClientResponse ToResponse(this TransportClient client) =>
        new(client.Id, client.PublicId, client.Name, client.TaxId, client.ContactPerson,
            client.Phone, client.Email, client.Address, client.IsActive, client.CreatedAt);

    internal static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
