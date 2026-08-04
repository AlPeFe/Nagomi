using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Features.Audit;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.ReferenceData;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.Api.Infrastructure.Persistence;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddNagomiPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Nagomi")
            ?? throw new InvalidOperationException("Connection string 'Nagomi' is required.");

        services.AddDbContext<NagomiDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseOpenIddict()
            .AddInterceptors(new UtcDateTimeOffsetSaveInterceptor()));
        services.AddScoped<INagomiDb>(provider => provider.GetRequiredService<NagomiDbContext>());
        services.AddScoped<IAuditHistoryQuery>(provider => provider.GetRequiredService<NagomiDbContext>());
        services.AddScoped<ITransportDb>(provider => provider.GetRequiredService<NagomiDbContext>());
        services.AddScoped<IProviderIntegrationDb>(provider => provider.GetRequiredService<NagomiDbContext>());
        return services;
    }
}
