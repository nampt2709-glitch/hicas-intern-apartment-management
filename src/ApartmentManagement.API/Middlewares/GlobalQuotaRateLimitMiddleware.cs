using System.Security.Claims;
using ApartmentManagement.Infrastructure;

namespace ApartmentManagement.Middlewares;

public sealed class GlobalQuotaRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalQuotaRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, QuotaRateLimiter quota)
    {
        // Only rate limit API traffic
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isAuth = context.User.Identity?.IsAuthenticated == true;

        // 9) Whole system: 1800 req/min
        if (!quota.TryConsume("global:system", 1800, TimeSpan.FromMinutes(1), out _, out _))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Global system rate limit exceeded." });
            return;
        }

        if (!isAuth)
        {
            // 1) Anonymous: 60 req/min/IP
            if (!quota.TryConsume($"global:anon:ip:{ip}", 60, TimeSpan.FromMinutes(1), out _, out _))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Anonymous IP rate limit exceeded." });
                return;
            }

            await _next(context);
            return;
        }

        // 9) Authenticated API total: 1200 req/min (shared bucket for authenticated traffic)
        if (!quota.TryConsume("global:authenticated", 1200, TimeSpan.FromMinutes(1), out _, out _))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Authenticated API rate limit exceeded." });
            return;
        }

        // 1) Per-user role-based
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var isAdmin = context.User.IsInRole("Admin");
        var perMinute = isAdmin ? 240 : 150;

        if (!quota.TryConsume($"global:acct:{userId}", perMinute, TimeSpan.FromMinutes(1), out _, out _))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Account rate limit exceeded." });
            return;
        }

        await _next(context);
    }
}

