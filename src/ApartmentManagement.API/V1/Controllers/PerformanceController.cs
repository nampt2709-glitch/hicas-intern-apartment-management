using System.Text.Json.Serialization;
using ApartmentManagement.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// Mục đích file: Endpoint giám sát hiệu năng máy chủ (CPU/RAM, throughput, độ trễ) và DTO JSON trả về cho Admin.

namespace ApartmentManagement.API.V1.Controllers;

// Controller hiệu năng — kế thừa ApiControllerBase; chỉ Admin; không bọc ApiResponseDto (trả Ok trực tiếp PerformanceReportJson).
public sealed class PerformanceController : ApiControllerBase
{
    private readonly PerformanceMetricsService _metrics;

    // Phụ thuộc inject: PerformanceMetricsService (snapshot metric đã thu thập middleware/tiến trình).
    public PerformanceController(PerformanceMetricsService metrics)
    {
        _metrics = metrics;
    }

    // GET: snapshot hiệu năng toàn process (CPU/RAM), cửa sổ 60s (số request, RPS, độ trễ TB, tỷ lệ lỗi lifetime) — map sang PerformanceReportJson.
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

// DTO JSON phẳng cho client — tên thuộc tính API là camelCase (JsonPropertyName).
public sealed class PerformanceReportJson
{
    // % CPU tiến trình hiện tại.
    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; init; }

    // RAM đã dùng (MB).
    [JsonPropertyName("memoryUsedMB")]
    public long MemoryUsedMB { get; init; }

    // Tổng RAM hệ thống (MB).
    [JsonPropertyName("totalMemoryMB")]
    public long TotalMemoryMB { get; init; }

    // Số request HTTP trong 60 giây gần nhất (không tính /performance).
    [JsonPropertyName("requestsInWindow")]
    public int RequestsInWindow { get; init; }

    // Request/giây trung bình trên cửa sổ 60 giây (≈ requestsInWindow / 60).
    [JsonPropertyName("throughputRps")]
    public double ThroughputRps { get; init; }

    // Độ trễ phản hồi trung bình (ms).
    [JsonPropertyName("avgLatencyMs")]
    public double AvgLatencyMs { get; init; }

    // Tỷ lệ lỗi (lifetime hoặc theo định nghĩa service).
    [JsonPropertyName("failRate")]
    public double FailRate { get; init; }
}
