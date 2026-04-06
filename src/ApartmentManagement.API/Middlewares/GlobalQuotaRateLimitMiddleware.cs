using System.Security.Claims;
using ApartmentManagement.Infrastructure;

namespace ApartmentManagement.Middlewares;

// Quota toàn cục trên /api: bucket hệ thống, ẩn danh theo IP, đã đăng nhập theo user + role (Admin cao hơn).
public sealed class GlobalQuotaRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalQuotaRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, QuotaRateLimiter quota)
    {
        // Bỏ qua không phải /api (Swagger, static...).
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isAuth = context.User.Identity?.IsAuthenticated == true;

        // Cửa sổ 1 phút: toàn hệ thống tối đa 1800 request.
        if (!quota.TryConsume("global:system", 1800, TimeSpan.FromMinutes(1), out _, out _))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Global system rate limit exceeded." });
            return;
        }

        if (!isAuth)
        {
            // Ẩn danh: 60 request/phút/IP.
            if (!quota.TryConsume($"global:anon:ip:{ip}", 60, TimeSpan.FromMinutes(1), out _, out _))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Anonymous IP rate limit exceeded." });
                return;
            }

            await _next(context);
            return;
        }

        // Đã đăng nhập: bucket chung 1200 request/phút cho toàn bộ traffic xác thực.
        if (!quota.TryConsume("global:authenticated", 1200, TimeSpan.FromMinutes(1), out _, out _))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Authenticated API rate limit exceeded." });
            return;
        }

        // Theo user: Admin 240/phút, user thường 150/phút.
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

