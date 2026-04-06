using System.Globalization;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.Performance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ApartmentManagement.Middlewares;

// Chẩn đoán hiệu năng qua header phản hồi (không đụng vào JSON body).
public static class ResponseDiagnosticsHeaders
{
    public const string DurationMs = "X-Response-Time-Ms";
    public const string QueryCount = "X-Db-Query-Count";
    public const string DbQueryTimeMs = "X-Db-Query-Time-Ms";
    public const string CacheBackend = "X-Cache-Backend";
    public const string CacheHit = "X-Cache-Hit";
    public const string CacheStatus = "X-Cache-Status";

    // Tên header: snapshot máy chủ cửa sổ lăn (cùng ý với GET /performance, trừ vài chỉ số).
    public const string ServerCpuPercent = "X-Server-Cpu-Percent";
    public const string ServerMemoryUsedMb = "X-Server-Memory-Used-Mb";
    public const string ServerTotalMemoryMb = "X-Server-Total-Memory-Mb";
    public const string ServerThroughputRps = "X-Server-Throughput-Rps";
    public const string ServerAvgLatencyMs = "X-Server-Avg-Latency-Ms";

    // Gắn thời gian request, số query DB, thời gian query, trạng thái cache từ RequestMetrics + ICacheService.
    public static void Apply(HttpContext context)
    {
        var metrics = context.RequestServices.GetRequiredService<RequestMetrics>();
        var cacheInfo = context.RequestServices.GetRequiredService<ICacheService>().GetCurrentCacheInfo();

        var headers = context.Response.Headers;
        headers[DurationMs] = metrics.ElapsedMilliseconds.ToString();
        headers[QueryCount] = metrics.DbQueryCount.ToString();
        headers[DbQueryTimeMs] = metrics.DbQueryElapsedMilliseconds.ToString();
        headers[CacheBackend] = cacheInfo.Backend.ToString();
        headers[CacheHit] = cacheInfo.Hit ? "true" : "false";
        headers[CacheStatus] = cacheInfo.Status;
    }

    // Snapshot rolling window (CPU, RAM, throughput, latency) — bỏ qua route /performance tránh vòng lặp.
    public static void ApplyPerformanceSnapshot(HttpContext context, PerformanceMetricsService metrics)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metrics);

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Contains("/performance", StringComparison.OrdinalIgnoreCase))
            return;

        var s = metrics.GetSnapshot();
        var headers = context.Response.Headers;
        var c = CultureInfo.InvariantCulture;
        headers[ServerCpuPercent] = s.CpuPercent.ToString("0.#", c);
        headers[ServerMemoryUsedMb] = s.MemoryUsedMB.ToString(c);
        headers[ServerTotalMemoryMb] = s.TotalMemoryMB.ToString(c);
        headers[ServerThroughputRps] = s.ThroughputRps.ToString("0.##", c);
        headers[ServerAvgLatencyMs] = s.AvgLatencyMs.ToString("0.#", c);
    }
}
