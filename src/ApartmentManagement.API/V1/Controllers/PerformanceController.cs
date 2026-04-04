using System.Text.Json.Serialization;
using ApartmentManagement.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.API.V1.Controllers;

public sealed class PerformanceController : ApiControllerBase
{
    private readonly PerformanceMetricsService _metrics;

    public PerformanceController(PerformanceMetricsService metrics)
    {
        _metrics = metrics;
    }

    /// <summary>Server-wide performance snapshot (process CPU/RAM, rolling 60s window: request count, avg RPS, avg latency, lifetime error rate).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public ActionResult<PerformanceReportJson> GetReport()
    {
        var s = _metrics.GetSnapshot();
        return Ok(new PerformanceReportJson
        {
            CpuPercent = s.CpuPercent,
            MemoryUsedMB = s.MemoryUsedMB,
            TotalMemoryMB = s.TotalMemoryMB,
            RequestsInWindow = s.RequestsInWindow,
            ThroughputRps = s.ThroughputRps,
            AvgLatencyMs = s.AvgLatencyMs,
            FailRate = s.FailRate
        });
    }
}

public sealed class PerformanceReportJson
{
    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; init; }

    [JsonPropertyName("memoryUsedMB")]
    public long MemoryUsedMB { get; init; }

    [JsonPropertyName("totalMemoryMB")]
    public long TotalMemoryMB { get; init; }

    /// <summary>HTTP requests recorded in the last 60s (excluding /performance).</summary>
    [JsonPropertyName("requestsInWindow")]
    public int RequestsInWindow { get; init; }

    /// <summary>Average requests per second over the rolling 60s window (= requestsInWindow / 60).</summary>
    [JsonPropertyName("throughputRps")]
    public double ThroughputRps { get; init; }

    [JsonPropertyName("avgLatencyMs")]
    public double AvgLatencyMs { get; init; }

    [JsonPropertyName("failRate")]
    public double FailRate { get; init; }
}
