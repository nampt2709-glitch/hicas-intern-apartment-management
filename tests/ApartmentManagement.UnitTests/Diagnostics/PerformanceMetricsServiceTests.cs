using ApartmentManagement.Performance;
using FluentAssertions;

namespace ApartmentManagement.UnitTests.Diagnostics;

public sealed class PerformanceMetricsServiceTests
{
    [Fact]
    public void RecordRequest_Ignores_Performance_Endpoint()
    {
        var sut = new PerformanceMetricsService();
        sut.RecordRequest("/api/v1.0/performance", 100, 200);
        var snap = sut.GetSnapshot();
        snap.RequestsInWindow.Should().Be(0);
        snap.ThroughputRps.Should().Be(0);
    }

    [Fact]
    public void GetSnapshot_Throughput_Is_Average_Over_60_Second_Window()
    {
        var sut = new PerformanceMetricsService();
        for (var i = 0; i < 12; i++)
            sut.RecordRequest("/api/v1.0/apartments", 10, 200);

        var snap = sut.GetSnapshot();
        snap.RequestsInWindow.Should().Be(12);
        snap.ThroughputRps.Should().BeApproximately(12 / 60.0, 0.01);
        snap.AvgLatencyMs.Should().Be(10.0);
    }

    [Fact]
    public void RecordRequest_Counts_5xx_As_Failed_For_FailRate()
    {
        var sut = new PerformanceMetricsService();
        sut.RecordRequest("/api/x", 5, 200);
        sut.RecordRequest("/api/x", 5, 500);
        var snap = sut.GetSnapshot();
        snap.FailRate.Should().Be(50.0);
    }
}
