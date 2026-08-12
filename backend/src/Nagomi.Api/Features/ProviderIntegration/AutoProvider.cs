using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Nagomi.Api.Features.ProviderIntegration;

/// <summary>
/// Options for the tenant acting as its own transport provider (self-execution).
/// </summary>
public sealed class AutoProviderOptions
{
    public const string SectionName = "ProviderIntegration:AutoProvider";

    public string ProviderCode { get; set; } = "SELF";
    public string ProviderName { get; set; } = "Propio (auto-proveedor)";
    public string QueueName { get; set; } = "nagomi.self";
    public string ContractCode { get; set; } = "SELF";
    public string ContractDescription { get; set; } = "Traslados ejecutados por la propia organización";
}

/// <summary>Ensures the tenant is registered as its own transport provider (self-execution).</summary>
public interface IAutoProviderProvisioner
{
    Task EnsureAsync(CancellationToken cancellationToken = default);
}

public sealed class AutoProviderProvisioner(
    IProviderIntegrationDb db,
    IOptions<AutoProviderOptions> options) : IAutoProviderProvisioner
{
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        var provider = await db.TransportProviders.SingleOrDefaultAsync(x => x.Code == o.ProviderCode, cancellationToken);
        if (provider is null)
        {
            provider = new TransportProvider
            {
                Code = o.ProviderCode,
                Name = o.ProviderName,
                QueueName = o.QueueName,
                IsActive = true
            };
            db.TransportProviders.Add(provider);
        }
        else
        {
            provider.Name = o.ProviderName;
            provider.QueueName = o.QueueName;
            provider.IsActive = true;
        }

        var contract = await db.TransportContracts.SingleOrDefaultAsync(x => x.Code == o.ContractCode, cancellationToken);
        if (contract is null)
        {
            contract = new TransportContract
            {
                Code = o.ContractCode,
                Description = o.ContractDescription,
                IsActive = true
            };
            db.TransportContracts.Add(contract);
        }
        else
        {
            contract.Description = o.ContractDescription;
            contract.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Ensure an active route from the self contract to the self provider.
        var route = await db.ProviderContractRoutes.SingleOrDefaultAsync(
            x => x.ContractId == contract.Id, cancellationToken);
        if (route is null)
        {
            db.ProviderContractRoutes.Add(new ProviderContractRoute
            {
                ContractId = contract.Id,
                ProviderId = provider.Id,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else if (!route.IsActive || route.ProviderId != provider.Id)
        {
            route.ProviderId = provider.Id;
            route.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
