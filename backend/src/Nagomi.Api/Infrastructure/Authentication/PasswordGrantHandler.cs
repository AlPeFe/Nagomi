using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Nagomi.Api.Infrastructure.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Nagomi.Api.Infrastructure.Authentication;

/// <summary>Handles the OpenIddict password grant for Nagomi web users.</summary>
public sealed class PasswordGrantHandler(UserManager<ApplicationUser> userManager)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        // Only handle password grants; other grant types continue through the pipeline.
        if (!string.Equals(context.Request.GrantType, OpenIddictConstants.GrantTypes.Password, StringComparison.Ordinal))
            return;

        var user = await userManager.FindByNameAsync(context.Request.Username ?? string.Empty);
        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, context.Request.Password ?? string.Empty))
        {
            context.Reject(
                error: OpenIddictConstants.Errors.InvalidGrant,
                description: "Las credenciales no son válidas o el usuario está desactivado.");
            return;
        }

        var principal = await CreatePrincipalAsync(user);
        principal.SetScopes(OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "nagomi-api");
        principal.SetAudiences("nagomi-api");
        principal.SetDestinations(claim =>
            claim.Type is OpenIddictConstants.Claims.Subject
                or OpenIddictConstants.Claims.Name
                or OpenIddictConstants.Claims.Email
                or OpenIddictConstants.Claims.Role
                or "display_name"
                ? [OpenIddictConstants.Destinations.AccessToken]
                : []);
        context.SignIn(principal);
    }

    private async Task<ClaimsPrincipal> CreatePrincipalAsync(ApplicationUser user)
    {
        var identity = new ClaimsIdentity(
            "Bearer",
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, user.UserName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(user.Email))
            identity.AddClaim(OpenIddictConstants.Claims.Email, user.Email);
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            identity.AddClaim("display_name", user.DisplayName);

        foreach (var role in await userManager.GetRolesAsync(user))
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);

        return new ClaimsPrincipal(identity);
    }
}
