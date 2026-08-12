using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Infrastructure.Authentication;
using Nagomi.Api.Infrastructure.PublicIds;

namespace Nagomi.Api.Features.Tenant;

public static class TenantSettingsEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/tenant")
            .WithTags("Tenant")
            .RequireAuthorization(UserAuthorizationPolicies.Admin);

        admin.MapGet("/capabilities", GetCapabilities);
        admin.MapPut("/capabilities", SetCapabilities);
        admin.MapGet("/clients", ListClients);
        admin.MapPost("/clients", CreateClient);
        admin.MapPut("/clients/{id:guid}", UpdateClient);

        return endpoints;
    }

    private static async Task<Ok<TenantSettingsResponse>> GetCapabilities(
        ITenantDb db, CancellationToken cancellationToken)
    {
        var settings = await EnsureSettings(db, cancellationToken);
        return TypedResults.Ok(ToResponse(settings));
    }

    private static async Task<Results<Ok<TenantSettingsResponse>, ValidationProblem>> SetCapabilities(
        TenantSettingsCommand command, ITenantDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (command is null) return TypedResults.ValidationProblem(Error("request", "Capabilities are required."));
        var settings = await EnsureSettings(db, cancellationToken);
        var caps = TenantCapabilities.None;
        if (command.PublishesRequests) caps |= TenantCapabilities.PublishesRequests;
        if (command.ExecutesTransports) caps |= TenantCapabilities.ExecutesTransports;
        if (command.HandlesEmergencies) caps |= TenantCapabilities.HandlesEmergencies;
        settings.Capabilities = caps;
        settings.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(settings));
    }

    private static async Task<Ok<IReadOnlyList<ClientResponse>>> ListClients(
        bool? includeInactive, ITenantDb db, CancellationToken cancellationToken)
    {
        var query = db.TransportClients.AsNoTracking();
        if (includeInactive is not true) query = query.Where(x => x.IsActive);
        var clients = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<ClientResponse>>(clients.Select(x => x.ToResponse()).ToList());
    }

    private static async Task<Results<Created<ClientResponse>, ValidationProblem>> CreateClient(
        UpsertClientCommand command, ITenantDb db, TimeProvider clock, IPublicIdGenerator ids,
        CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
        var now = clock.GetUtcNow();
        var client = new TransportClient
        {
            PublicId = await ids.NextAsync("CLI", cancellationToken),
            Name = command.Name.Trim(),
            TaxId = ClientMapping.Clean(command.TaxId),
            ContactPerson = ClientMapping.Clean(command.ContactPerson),
            Phone = ClientMapping.Clean(command.Phone),
            Email = ClientMapping.Clean(command.Email),
            Address = ClientMapping.Clean(command.Address),
            IsActive = command.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TransportClients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/tenant/clients/{client.Id}", client.ToResponse());
    }

    private static async Task<Results<Ok<ClientResponse>, NotFound, ValidationProblem>> UpdateClient(
        Guid id, UpsertClientCommand command, ITenantDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var client = await db.TransportClients.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (client is null) return TypedResults.NotFound();
        var errors = Validate(command);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
        client.Name = command.Name.Trim();
        client.TaxId = ClientMapping.Clean(command.TaxId);
        client.ContactPerson = ClientMapping.Clean(command.ContactPerson);
        client.Phone = ClientMapping.Clean(command.Phone);
        client.Email = ClientMapping.Clean(command.Email);
        client.Address = ClientMapping.Clean(command.Address);
        client.IsActive = command.IsActive;
        client.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(client.ToResponse());
    }

    private static async Task<TenantSettings> EnsureSettings(ITenantDb db, CancellationToken cancellationToken)
    {
        var settings = await db.TenantSettings.SingleOrDefaultAsync(x => x.Id == TenantSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = new TenantSettings();
            db.TenantSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }
        return settings;
    }

    private static TenantSettingsResponse ToResponse(TenantSettings settings)
    {
        var caps = settings.Capabilities;
        return new TenantSettingsResponse(
            caps.HasFlag(TenantCapabilities.PublishesRequests),
            caps.HasFlag(TenantCapabilities.ExecutesTransports),
            caps.HasFlag(TenantCapabilities.HandlesEmergencies));
    }

    private static Dictionary<string, string[]> Validate(UpsertClientCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 250)
            errors["name"] = ["Name is required and must not exceed 250 characters."];
        if (command.TaxId?.Trim().Length > 50) errors["taxId"] = ["Tax identifier must not exceed 50 characters."];
        return errors;
    }

    private static Dictionary<string, string[]> Error(string key, string message) => new() { [key] = [message] };
}
