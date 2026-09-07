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
    /// <summary>Claim carried on the bootstrap admin's token so endpoints can enforce onboarding.</summary>
    public const string MustChangePasswordClaim = "must_change_password";

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        // Only handle password grants; other grant types continue through the pipeline.
        if (!string.Equals(context.Request.GrantType, OpenIddictConstants.GrantTypes.Password, StringComparison.Ordinal))
            return;

        var user = await userManager.FindByNameAsync(context.Request.Username ?? string.Empty);
        if (user is null || !user.IsActive)
        {
            await RejectInvalidAsync(context, user);
            return;
        }

        // Lockout: reject while locked out; count failures; reset on success.
        if (await userManager.IsLockedOutAsync(user))
        {
            context.Reject(
                error: OpenIddictConstants.Errors.InvalidGrant,
                description: "Cuenta bloqueada temporalmente por demasiados intentos fallidos. Inténtalo de nuevo en unos minutos.");
            return;
        }

        if (!await userManager.CheckPasswordAsync(user, context.Request.Password ?? string.Empty))
        {
            await userManager.AccessFailedAsync(user);
            context.Reject(
                error: OpenIddictConstants.Errors.InvalidGrant,
                description: "Las credenciales no son válidas o el usuario está desactivado.");
            return;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var principal = await CreatePrincipalAsync(user);
        principal.SetScopes(OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "nagomi-api");
        principal.SetAudiences("nagomi-api");
        principal.SetDestinations(claim =>
            claim.Type is OpenIddictConstants.Claims.Subject
                or OpenIddictConstants.Claims.Name
                or OpenIddictConstants.Claims.Email
                or OpenIddictConstants.Claims.Role
                or "display_name"
                or MustChangePasswordClaim
                ? [OpenIddictConstants.Destinations.AccessToken]
                : []);
        context.SignIn(principal);
    }

    private async Task RejectInvalidAsync(OpenIddictServerEvents.HandleTokenRequestContext context, ApplicationUser? user)
    {
        if (user is not null)
        {
            // Unknown usernames don't reveal account existence; known-but-inactive still count
            // against lockout only when the password was wrong. Keep the generic error either way.
            await userManager.AccessFailedAsync(user);
        }

        context.Reject(
            error: OpenIddictConstants.Errors.InvalidGrant,
            description: "Las credenciales no son válidas o el usuario está desactivado.");
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
        if (user.MustChangePassword)
            identity.AddClaim(MustChangePasswordClaim, "true");

        foreach (var role in await userManager.GetRolesAsync(user))
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);

        return new ClaimsPrincipal(identity);
    }
}
