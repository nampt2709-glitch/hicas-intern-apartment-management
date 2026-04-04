using Serilog.Context;

namespace ApartmentManagement.Middlewares;

/// <summary>
/// Ensures every request has a stable correlation id and flows it into logs and response headers.
/// </summary>
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
        // Prefer client-supplied correlation id; otherwise fall back to TraceIdentifier or a new GUID.
        var correlationId = GetOrCreateCorrelationId(context);

        // Keep ASP.NET trace id aligned with correlation id for consistent diagnostics.
        context.TraceIdentifier = correlationId;

        // Expose correlation id on HttpContext for downstream handlers.
        context.Items[ItemKey] = correlationId;
        // Ensure the header is present even if later middleware clears/rebuilds the response.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Enrich Serilog scope so all logs for this request carry the correlation id property.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

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

