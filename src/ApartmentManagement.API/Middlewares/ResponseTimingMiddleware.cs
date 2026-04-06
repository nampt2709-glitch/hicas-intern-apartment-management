using ApartmentManagement.Performance;

namespace ApartmentManagement.Middlewares;

// Reset RequestMetrics mỗi request; khi response bắt đầu — áp dụng header thời gian + DB + cache.
public sealed class ResponseTimingMiddleware
{
    private readonly RequestDelegate _next;

    public ResponseTimingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var metrics = context.RequestServices.GetRequiredService<RequestMetrics>();
        metrics.Reset();

        context.Response.OnStarting(() =>
        {
            ResponseDiagnosticsHeaders.Apply(context);
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
