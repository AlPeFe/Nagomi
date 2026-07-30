using Microsoft.Extensions.Options;
using Nagomi.Api.Features.Audit;

namespace Nagomi.Api.Infrastructure.Identity;

public sealed class SimulatedIdentityOptions
{
    public const string SectionName = "SimulatedIdentity";

    public string UserIdentifier { get; set; } = "nagomi-user";
    public string UserDisplayName { get; set; } = "Nagomi User";
    public string RequestingOrganizationIdentifier { get; set; } = "nagomi-organization";
    public string RequestingOrganizationName { get; set; } = "Nagomi Requesting Organization";
}

public interface ISimulatedIdentity
{
    string UserIdentifier { get; }
    string UserDisplayName { get; }
    string RequestingOrganizationIdentifier { get; }
    string RequestingOrganizationName { get; }
    AuditActor AuditActor { get; }
}

internal sealed class SimulatedIdentity(IOptions<SimulatedIdentityOptions> options) : ISimulatedIdentity
{
    private readonly SimulatedIdentityOptions _options = options.Value;

    public string UserIdentifier => _options.UserIdentifier;
    public string UserDisplayName => _options.UserDisplayName;
    public string RequestingOrganizationIdentifier => _options.RequestingOrganizationIdentifier;
    public string RequestingOrganizationName => _options.RequestingOrganizationName;
    public AuditActor AuditActor => AuditActor.ForSimulatedUser(UserIdentifier, UserDisplayName);
}

public static class SimulatedIdentityServiceExtensions
{
    public static IServiceCollection AddSimulatedIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SimulatedIdentityOptions>()
            .Bind(configuration.GetSection(SimulatedIdentityOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserIdentifier), "A simulated user identifier is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserDisplayName), "A simulated user display name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RequestingOrganizationIdentifier), "A requesting organization identifier is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RequestingOrganizationName), "A requesting organization name is required.")
            .ValidateOnStart();
        services.AddSingleton<ISimulatedIdentity, SimulatedIdentity>();
        return services;
    }
}
