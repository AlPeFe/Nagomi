using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.Tenant;

namespace Nagomi.Api.Infrastructure.Persistence;

public static class TenantSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ITenantDb>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var settings = await db.TenantSettings.SingleOrDefaultAsync(x => x.Id == TenantSettings.SingletonId);
        if (settings is null)
        {
            db.TenantSettings.Add(new TenantSettings { UpdatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync();
        }

        // When the tenant can execute its own transports, make sure it is
        // registered as a provider so self-execution routes work out of the box.
        var provisioner = scope.ServiceProvider.GetRequiredService<IAutoProviderProvisioner>();
        await provisioner.EnsureAsync();
    }
}
