using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Infrastructure.Authentication;
using Nagomi.Api.Infrastructure.Identity;
using OpenIddict.Abstractions;

namespace Nagomi.Api.Features.UserAdministration;

public sealed record CurrentUserInfo(
    string Id,
    string Name,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    bool OnboardingRequired);

/// <summary>Body of the first-run onboarding: create the real administrator that replaces the bootstrap admin.</summary>
public sealed record OnboardingRequest(string DisplayName, string Email, string UserName, string Password);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").RequireAuthorization().WithTags("User authentication");
        group.MapGet("/me", Me);
        group.MapPost("/onboarding", Onboard);
        group.MapPost("/logout", () => TypedResults.NoContent());
        return endpoints;
    }

    private static Results<Ok<CurrentUserInfo>, UnauthorizedHttpResult> Me(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(OpenIddictConstants.Claims.Subject)
                 ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = principal.FindFirstValue(OpenIddictConstants.Claims.Name)
                   ?? principal.FindFirstValue(ClaimTypes.Name);
        if (id is null || name is null)
            return TypedResults.Unauthorized();

        var roles = principal.FindAll(OpenIddictConstants.Claims.Role)
            .Select(x => x.Value)
            .Concat(principal.FindAll(ClaimTypes.Role).Select(x => x.Value))
            .Distinct()
            .ToArray();

        var onboarding = principal.HasClaim(PasswordGrantHandler.MustChangePasswordClaim, "true");

        return TypedResults.Ok(new CurrentUserInfo(
            id,
            name,
            principal.FindFirstValue(OpenIddictConstants.Claims.Email) ?? principal.FindFirstValue(ClaimTypes.Email),
            principal.FindFirstValue("display_name"),
            roles,
            onboarding));
    }

    /// <summary>
    /// First-run onboarding: only callable by the bootstrap admin (admin / Admin, MustChangePassword).
    /// Creates the real administrator account, then deactivates the bootstrap account so it can no
    /// longer log in. The caller must sign in again with the new credentials.
    /// </summary>
    private static async Task<Results<Ok, UnauthorizedHttpResult, Conflict, ValidationProblem>> Onboard(
        OnboardingRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var bootstrapId = principal.FindFirstValue(OpenIddictConstants.Claims.Subject)
                          ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (bootstrapId is null)
            return TypedResults.Unauthorized();

        var bootstrap = await userManager.Users.SingleOrDefaultAsync(x => x.Id.ToString() == bootstrapId, cancellationToken);
        // Only the bootstrap account can run onboarding.
        if (bootstrap is null || !bootstrap.MustChangePassword || !bootstrap.IsActive)
            return TypedResults.Conflict();

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            errors["displayName"] = ["El nombre es obligatorio."];
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            errors["email"] = ["Introduce un correo electrónico válido."];
        if (string.IsNullOrWhiteSpace(request.UserName))
            errors["userName"] = ["El nombre de usuario es obligatorio."];
        if (string.IsNullOrWhiteSpace(request.Password))
            errors["password"] = ["La contraseña es obligatoria."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var existing = await userManager.FindByEmailAsync(request.Email.Trim());
        if (existing is not null && existing.Id.ToString() != bootstrapId)
            return TypedResults.Conflict();

        var admin = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var create = await userManager.CreateAsync(admin, request.Password);
        if (!create.Succeeded)
        {
            var problems = create.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            return TypedResults.ValidationProblem(problems);
        }

        await userManager.AddToRoleAsync(admin, NagomiRoles.Admin);

        // Deactivate the bootstrap account: it can no longer log in.
        bootstrap.MustChangePassword = false;
        bootstrap.IsActive = false;
        await userManager.UpdateAsync(bootstrap);

        return TypedResults.Ok();
    }
}
