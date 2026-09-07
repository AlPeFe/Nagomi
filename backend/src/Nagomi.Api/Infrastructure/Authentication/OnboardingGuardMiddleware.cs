using System.Text.Json;

namespace Nagomi.Api.Infrastructure.Authentication;

/// <summary>
/// Enforces the first-run onboarding: the bootstrap admin (admin / Admin) carries the
/// MustChangePasswordClaim and can ONLY reach the auth endpoints needed to complete onboarding
/// (/api/auth/me, /api/auth/onboarding, /connect/token) plus health checks. Every other endpoint
/// returns 403 until a real administrator account is created and the bootstrap is deactivated.
/// </summary>
public sealed class OnboardingGuardMiddleware
{
    private static readonly HashSet<string> AllowedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/me",
        "/api/auth/onboarding",
        "/api/auth/logout",
        "/connect/token",
        "/health",
        "/ready",
    };

    private readonly RequestDelegate _next;

    public OnboardingGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        var mustOnboard = user.Identity?.IsAuthenticated == true &&
                          user.HasClaim(PasswordGrantHandler.MustChangePasswordClaim, "true");

        if (mustOnboard && !AllowedPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "onboarding_required",
                message = "La cuenta inicial debe completar el alta del administrador antes de usar la aplicación."
            }));
            return;
        }

        await _next(context);
    }
}
