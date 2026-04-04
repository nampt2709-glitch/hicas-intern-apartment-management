using System.Diagnostics;
using ApartmentManagement.Performance;

namespace ApartmentManagement.Middlewares;

public sealed class PerformanceMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public PerformanceMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, PerformanceMetricsService metrics)
    {
        var sw = Stopwatch.StartNew();
        var recorded = false;

        void Record()
        {
            if (recorded)
                return;
            recorded = true;
            sw.Stop();
            var path = context.Request.Path.Value ?? string.Empty;
            metrics.RecordRequest(path, (int)sw.ElapsedMilliseconds, context.Response.StatusCode);
        }

        context.Response.OnStarting(() =>
        {
            Record();
            var path = context.Request.Path.Value ?? string.Empty;
            if (!IsPerformancePath(path))
                ResponseDiagnosticsHeaders.ApplyPerformanceSnapshot(context, metrics);
            return Task.CompletedTask;
        });

        try
        {
            await _next(context);
        }
        finally
        {
            if (!recorded)
                Record();
        }
    }

    private static bool IsPerformancePath(string path) =>
        path.Contains("/performance", StringComparison.OrdinalIgnoreCase);
}
