using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Infrastructure.Identity;

namespace Nagomi.Api.Features.UserAdministration;

public sealed record AdminUserRow(
    Guid Id,
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateUserCommand(string Email, string Password, string? Role = null);

public sealed record UpdateUserCommand(string? Role = null, bool? IsActive = null, string? Password = null);

public static class UserAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapUserAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/users")
            .WithTags("User administration")
            .RequireAuthorization(Nagomi.Api.Infrastructure.Authentication.UserAuthorizationPolicies.Admin);
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<AdminUserRow>>> List(
        UserManager<ApplicationUser> userManager, CancellationToken cancellationToken)
    {
        var users = await userManager.Users.AsNoTracking()
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);
        var rows = new List<AdminUserRow>(users.Count);
        foreach (var user in users)
        {
            rows.Add(new AdminUserRow(
                user.Id,
                user.Email ?? user.UserName ?? string.Empty,
                user.DisplayName,
                (await userManager.GetRolesAsync(user)).ToArray(),
                user.IsActive,
                user.CreatedAt));
        }
        return TypedResults.Ok<IReadOnlyList<AdminUserRow>>(rows);
    }

    private static async Task<Results<Created<AdminUserRow>, ValidationProblem>> Create(
        CreateUserCommand command,
        UserManager<ApplicationUser> userManager,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var email = command.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(command.Password))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["Email and password are required."]
            });

        var role = string.IsNullOrWhiteSpace(command.Role) ? NagomiRoles.Default : command.Role.Trim();
        if (!NagomiRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = [$"Role must be one of: {string.Join(", ", NagomiRoles.All)}."]
            });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = null,
            IsActive = true,
            CreatedAt = clock.GetUtcNow()
        };
        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
            return IdentityErrors(result);

        var roleResult = await userManager.AddToRoleAsync(user, role.ToLowerInvariant());
        if (!roleResult.Succeeded)
            return IdentityErrors(roleResult);

        return TypedResults.Created($"/api/admin/users/{user.Id}", new AdminUserRow(
            user.Id, user.Email ?? user.UserName!, user.DisplayName, [role.ToLowerInvariant()], user.IsActive, user.CreatedAt));
    }

    private static async Task<Results<Ok<AdminUserRow>, NotFound, ValidationProblem, Conflict<string>>> Update(
        Guid id,
        UpdateUserCommand command,
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return TypedResults.NotFound();

        if (command.IsActive.HasValue)
            user.IsActive = command.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(command.Password))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await userManager.ResetPasswordAsync(user, resetToken, command.Password);
            if (!passwordResult.Succeeded)
                return IdentityErrors(passwordResult);
        }

        if (!string.IsNullOrWhiteSpace(command.Role))
        {
            var role = command.Role.Trim().ToLowerInvariant();
            if (!NagomiRoles.All.Contains(role, StringComparer.Ordinal))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = [$"Role must be one of: {string.Join(", ", NagomiRoles.All)}."]
                });

            var currentRoles = await userManager.GetRolesAsync(user);
            var updateResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!updateResult.Succeeded) return IdentityErrors(updateResult);
            updateResult = await userManager.AddToRoleAsync(user, role);
            if (!updateResult.Succeeded) return IdentityErrors(updateResult);

            if (role == NagomiRoles.Admin && principal.FindFirstValue(ClaimTypes.NameIdentifier) == user.Id.ToString())
                return TypedResults.Conflict("No puedes quitarte el rol de administrador a ti mismo.");
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return IdentityErrors(result);

        return TypedResults.Ok(new AdminUserRow(
            user.Id, user.Email ?? user.UserName!, user.DisplayName,
            (await userManager.GetRolesAsync(user)).ToArray(), user.IsActive, user.CreatedAt));
    }

    private static async Task<Results<NoContent, NotFound, Conflict<string>>> Delete(
        Guid id,
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return TypedResults.NotFound();
        if (principal.FindFirstValue(ClaimTypes.NameIdentifier) == user.Id.ToString())
            return TypedResults.Conflict("No puedes eliminar tu propia cuenta.");

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return TypedResults.Conflict(string.Join("; ", result.Errors.Select(x => x.Description)));

        return TypedResults.NoContent();
    }

    private static ValidationProblem IdentityErrors(IdentityResult result) =>
        TypedResults.ValidationProblem(result.Errors.ToDictionary(
            x => x.Code,
            x => new[] { x.Description }));
}
