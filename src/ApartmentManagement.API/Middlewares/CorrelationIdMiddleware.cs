using Serilog.Context;

namespace ApartmentManagement.Middlewares;

// Gắn CorrelationId ổn định cho mỗi request, đưa vào log Serilog và header phản hồi.
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Ưu tiên header client; không có thì dùng TraceIdentifier hoặc GUID mới.
        var correlationId = GetOrCreateCorrelationId(context);

        // Đồng bộ TraceIdentifier với correlation để diagnostic nhất quán.
        context.TraceIdentifier = correlationId;

        // Lưu vào Items cho handler/middleware sau.
        context.Items[ItemKey] = correlationId;
        // Đảm bảo header có trên response khi bắt đầu gửi (tránh mất sau khi middleware khác xử lý).
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Mọi log trong scope request đều có property CorrelationId.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    // Đọc header → fallback trace → GUID.
    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
            !string.IsNullOrWhiteSpace(existing))
        {
            return existing.ToString().Trim();
        }

        var traceId = context.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            return traceId;
        }

        return Guid.NewGuid().ToString("N");
    }
}

