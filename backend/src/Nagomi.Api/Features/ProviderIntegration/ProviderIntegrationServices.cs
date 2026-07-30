namespace Nagomi.Api.Features.ProviderIntegration;

public static class ProviderIntegrationServices
{
    public static IServiceCollection AddProviderIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ProviderAuthorizationPolicies.Administration, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim(ProviderClaimTypes.Role, ProviderClaimTypes.Administrator));
            options.AddPolicy(ProviderAuthorizationPolicies.Operations, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireClaim(ProviderClaimTypes.Role,
                        ProviderClaimTypes.Operator, ProviderClaimTypes.Administrator));
        });
        services.Configure<ProviderRabbitMqOptions>(configuration.GetSection(ProviderRabbitMqOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProviderAuthorizer, OpenIddictClaimsProviderAuthorizer>();
        services.AddScoped<IProviderOutbox, ProviderOutbox>();
        services.AddScoped<INotificationRetrievalTracker, NotificationRetrievalTracker>();
        services.AddScoped<IProviderCommandIdempotency, ProviderCommandIdempotency>();
        services.AddSingleton<IProviderNotificationPublisher, RabbitMqProviderNotificationPublisher>();
        services.AddHostedService<ProviderOutboxWorker>();
        return services;
    }
}
