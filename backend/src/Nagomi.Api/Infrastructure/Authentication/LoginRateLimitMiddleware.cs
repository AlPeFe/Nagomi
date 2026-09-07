using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Nagomi.Api.Infrastructure.Authentication;

public sealed class LoginRateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    /// <summary>Max password-grant attempts per IP per minute. 0 disables the limiter (dev/tests).</summary>
    public int LoginPerMinute { get; set; } = 10;
}

/// <summary>
/// Fixed-window rate limiter per client IP for the password token endpoint (/connect/token).
/// ASP.NET's endpoint rate limiting cannot target the OpenIddict middleware endpoint directly,
/// so this small middleware guards it. Complements the per-account lockout in PasswordGrantHandler.
/// </summary>
public sealed class LoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _perMinute;
    private readonly ConcurrentDictionary<string, (FixedWindowRateLimiter Limiter, DateTime LastUsed)> _limiters = new();

    public LoginRateLimitMiddleware(RequestDelegate next, IOptions<LoginRateLimitingOptions> options)
    {
        _next = next;
        _perMinute = Math.Max(0, options.Value.LoginPerMinute);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_perMinute <= 0 || !HttpMethods.IsPost(context.Request.Method) ||
            !string.Equals(context.Request.Path, "/connect/token", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;
        var (limiter, _) = _limiters.GetOrAdd(ip, _ => (
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = _perMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }),
            now));

        _limiters[ip] = (limiter, now);
        CleanupIfLarge(now);

        using var lease = await limiter.AcquireAsync(1);
        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsync("Demasiados intentos de acceso. Espera un minuto e inténtalo de nuevo.");
            return;
        }

        await _next(context);
    }

    /// <summary>Prevents unbounded dictionary growth: prune entries idle for over 10 minutes.</summary>
    private void CleanupIfLarge(DateTime now)
    {
        if (_limiters.Count < 1024)
            return;
        foreach (var entry in _limiters)
        {
            if ((now - entry.Value.LastUsed) > TimeSpan.FromMinutes(10))
                _limiters.TryRemove(entry.Key, out _);
        }
    }
}
