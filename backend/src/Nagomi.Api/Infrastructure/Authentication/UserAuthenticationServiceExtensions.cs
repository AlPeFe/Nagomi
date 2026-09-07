using Microsoft.AspNetCore.Identity;
using Nagomi.Api.Infrastructure.Identity;
using Nagomi.Api.Infrastructure.Persistence;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Nagomi.Api.Infrastructure.Authentication;

public sealed class UserAuthenticationOptions
{
    public const string SectionName = "Authentication:Users";

    public string? AdminEmail { get; set; }
    /// <summary>Legacy: only used when no user exists AND AdminEmail differs from the bootstrap.</summary>
    public string? AdminPassword { get; set; }
    /// <summary>Credentials for the first-run bootstrap admin (defaults to admin / Admin).</summary>
    public string BootstrapUserName { get; set; } = "admin";
    public string BootstrapPassword { get; set; } = "Admin";
}

public static class UserAuthorizationPolicies
{
    public const string Web = "NagomiWeb";
    public const string Admin = "NagomiUserAdministration";
}

public static class UserAuthenticationServiceExtensions
{
    public static IServiceCollection AddUserAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(UserAuthenticationOptions.SectionName);
        services.Configure<UserAuthenticationOptions>(section);

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // NIST 800-63B / ISO 27001 A.9.4.3: minimum length 12; composition rules are
                // deliberately relaxed (length beats arbitrary composition).
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 12;

                // Brute-force protection: lock after 10 failed attempts for 15 minutes.
                // Enforced manually in PasswordGrantHandler (OpenIddict custom grant).
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<NagomiDbContext>();

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.AllowPasswordFlow()
                    .AcceptAnonymousClients()
                    .AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.HandleTokenRequestContext>(builder =>
                        builder
                            .SetOrder(OpenIddict.Server.OpenIddictServerHandlers.Exchange.ValidateClientIdParameter.Descriptor.Order - 500)
                            .UseScopedHandler<PasswordGrantHandler>());
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(UserAuthorizationPolicies.Web,
                policy => policy.RequireRole(NagomiRoles.Admin, NagomiRoles.Default));
            options.AddPolicy(UserAuthorizationPolicies.Admin,
                policy => policy.RequireRole(NagomiRoles.Admin));
        });

        return services;
    }
}
