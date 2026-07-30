using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ProviderIntegration;

public sealed record UpsertProviderRequest(string Code, string Name, string QueueName, bool IsActive = true);
public sealed record UpsertContractRequest(string Code, string Description, bool IsActive = true);
public sealed record SetProviderRouteRequest(Guid ProviderId, bool IsActive = true);

public static class ProviderAdministrationEndpoints
{
    public static async Task<IResult> GetProvidersAsync(IProviderIntegrationDb db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.TransportProviders.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken));

    public static async Task<IResult> CreateProviderAsync(
        UpsertProviderRequest request, IProviderIntegrationDb db, CancellationToken cancellationToken)
    {
        if (!Valid(request.Code, request.Name, request.QueueName)) return Invalid();
        var provider = new TransportProvider
        {
            Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(),
            QueueName = request.QueueName.Trim(), IsActive = request.IsActive
        };
        db.TransportProviders.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/provider-administration/providers/{provider.Id}", provider);
    }

    public static async Task<IResult> UpdateProviderAsync(
        Guid id, UpsertProviderRequest request, IProviderIntegrationDb db, CancellationToken cancellationToken)
    {
        if (!Valid(request.Code, request.Name, request.QueueName)) return Invalid();
        var provider = await db.TransportProviders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (provider is null) return TypedResults.NotFound();
        provider.Code = request.Code.Trim().ToUpperInvariant();
        provider.Name = request.Name.Trim();
        provider.QueueName = request.QueueName.Trim();
        provider.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(provider);
    }

    public static async Task<IResult> GetContractsAsync(IProviderIntegrationDb db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.TransportContracts.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken));

    public static async Task<IResult> CreateContractAsync(
        UpsertContractRequest request, IProviderIntegrationDb db, CancellationToken cancellationToken)
    {
        if (!Valid(request.Code, request.Description)) return Invalid();
        var contract = new TransportContract
        {
            Code = request.Code.Trim().ToUpperInvariant(), Description = request.Description.Trim(), IsActive = request.IsActive
        };
        db.TransportContracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/provider-administration/contracts/{contract.Id}", contract);
    }

    public static async Task<IResult> UpdateContractAsync(
        Guid id, UpsertContractRequest request, IProviderIntegrationDb db, CancellationToken cancellationToken)
    {
        if (!Valid(request.Code, request.Description)) return Invalid();
        var contract = await db.TransportContracts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (contract is null) return TypedResults.NotFound();
        contract.Code = request.Code.Trim().ToUpperInvariant();
        contract.Description = request.Description.Trim();
        contract.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(contract);
    }

    public static async Task<IResult> SetRouteAsync(
        Guid contractId, SetProviderRouteRequest request, IProviderIntegrationDb db,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (!await db.TransportContracts.AnyAsync(x => x.Id == contractId, cancellationToken) ||
            !await db.TransportProviders.AnyAsync(x => x.Id == request.ProviderId, cancellationToken))
            return TypedResults.NotFound();

        var routes = await db.ProviderContractRoutes.Where(x => x.ContractId == contractId).ToListAsync(cancellationToken);
        foreach (var route in routes) route.IsActive = false;
        var selected = routes.SingleOrDefault(x => x.ProviderId == request.ProviderId);
        if (selected is null)
        {
            selected = new ProviderContractRoute
            {
                ContractId = contractId, ProviderId = request.ProviderId, CreatedAt = timeProvider.GetUtcNow()
            };
            db.ProviderContractRoutes.Add(selected);
        }
        selected.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(selected);
    }

    private static bool Valid(params string[] values) => values.All(x => !string.IsNullOrWhiteSpace(x));
    private static IResult Invalid() => TypedResults.ValidationProblem(
        new Dictionary<string, string[]> { ["request"] = ["Code, name/description, and queue are required where applicable."] });
}
