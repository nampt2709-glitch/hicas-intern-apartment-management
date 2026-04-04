using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ApartmentManagement.Performance;

/// <summary>Thread-safe in-memory HTTP performance counters and process/system resource snapshot.</summary>
public sealed class PerformanceMetricsService
{
    private const int RollingWindowSeconds = 60;

    private readonly object _cpuLock = new();
    private readonly object _windowLock = new();
    private readonly Queue<WindowEntry> _window = new();

    private long _lifetimeTotal;
    private long _lifetimeFailed;

    private static readonly Process CurrentProcess = Process.GetCurrentProcess();
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuSampleUtc = DateTime.MinValue;
    private double _lastCpuPercent;

    public void RecordRequest(string path, int latencyMs, int statusCode)
    {
        if (ShouldExcludePath(path))
            return;

        var failed = statusCode == 0 || statusCode >= 500;
        Interlocked.Increment(ref _lifetimeTotal);
        if (failed)
            Interlocked.Increment(ref _lifetimeFailed);

        var now = DateTime.UtcNow;
        lock (_windowLock)
        {
            TrimWindow(now);
            _window.Enqueue(new WindowEntry(now, latencyMs));
        }
    }

    public PerformanceReportSnapshot GetSnapshot()
    {
        var now = DateTime.UtcNow;
        int windowCount;
        double avgLatencyMs;
        double throughputRps;

        lock (_windowLock)
        {
            TrimWindow(now);
            windowCount = _window.Count;
            if (windowCount == 0)
            {
                avgLatencyMs = 0;
                // Average requests/sec over the full rolling window (stable for sparse traffic; avoids rounding to 0 when only a few requests span a long interval).
                throughputRps = 0;
            }
            else
            {
                long sumLatency = 0;
                foreach (var e in _window)
                    sumLatency += e.LatencyMs;
                avgLatencyMs = sumLatency / (double)windowCount;
                throughputRps = windowCount / (double)RollingWindowSeconds;
            }
        }

        CurrentProcess.Refresh();
        var workingSetMb = CurrentProcess.WorkingSet64 / (1024.0 * 1024.0);

        double cpuPercent;
        lock (_cpuLock)
        {
            var cpuNow = CurrentProcess.TotalProcessorTime;
            if (_lastCpuSampleUtc == DateTime.MinValue)
            {
                _lastCpuTime = cpuNow;
                _lastCpuSampleUtc = now;
                cpuPercent = 0;
            }
            else
            {
                var wallMs = (now - _lastCpuSampleUtc).TotalMilliseconds;
                if (wallMs < 50)
                    cpuPercent = _lastCpuPercent;
                else
                {
                    var cpuMs = (cpuNow - _lastCpuTime).TotalMilliseconds;
                    var logical = Environment.ProcessorCount;
                    if (logical < 1) logical = 1;
                    _lastCpuPercent = Math.Clamp(cpuMs / (wallMs * logical) * 100.0, 0, 100);
                    _lastCpuTime = cpuNow;
                    _lastCpuSampleUtc = now;
                    cpuPercent = _lastCpuPercent;
                }
            }
        }

        var totalMemMb = TryGetTotalPhysicalMemoryMb();
        var total = GetInterlocked(ref _lifetimeTotal);
        var failed = GetInterlocked(ref _lifetimeFailed);
        var failRate = total == 0 ? 0 : failed * 100.0 / total;

        return new PerformanceReportSnapshot(
            Math.Round(cpuPercent, 1),
            (long)Math.Round(workingSetMb),
            totalMemMb is > 0 ? (long)Math.Round(totalMemMb.Value) : 0L,
            windowCount,
            Math.Round(throughputRps, 2),
            Math.Round(avgLatencyMs, 1),
            Math.Round(failRate, 2));
    }

    private static long GetInterlocked(ref long field) => Interlocked.Read(ref field);

    private void TrimWindow(DateTime now)
    {
        var cutoff = now.AddSeconds(-RollingWindowSeconds);
        while (_window.Count > 0 && _window.Peek().AtUtc < cutoff)
            _window.Dequeue();
    }

    private static bool ShouldExcludePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return path.Contains("/performance", StringComparison.OrdinalIgnoreCase);
    }

    private static double? TryGetTotalPhysicalMemoryMb()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal))
                        continue;
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                        return kb / 1024.0;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var stat = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
                if (GlobalMemoryStatusEx(ref stat))
                    return stat.TotalPhys / (1024.0 * 1024.0);
            }
        }
        catch
        {
            /* best-effort */
        }

        return null;
    }

    private readonly record struct WindowEntry(DateTime AtUtc, int LatencyMs);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
}

public sealed record PerformanceReportSnapshot(
    double CpuPercent,
    long MemoryUsedMB,
    long TotalMemoryMB,
    int RequestsInWindow,
    double ThroughputRps,
    double AvgLatencyMs,
    double FailRate);
