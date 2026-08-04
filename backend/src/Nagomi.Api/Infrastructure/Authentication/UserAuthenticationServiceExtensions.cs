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
    public string? AdminPassword { get; set; }
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
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
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
