using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using OpenIddict.Abstractions;

namespace Nagomi.Api.Features.UserAdministration;

public sealed record CurrentUserInfo(
    string Id,
    string Name,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> Roles);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").RequireAuthorization().WithTags("User authentication");
        group.MapGet("/me", Me);
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

        return TypedResults.Ok(new CurrentUserInfo(
            id,
            name,
            principal.FindFirstValue(OpenIddictConstants.Claims.Email) ?? principal.FindFirstValue(ClaimTypes.Email),
            principal.FindFirstValue("display_name"),
            roles));
    }
}
