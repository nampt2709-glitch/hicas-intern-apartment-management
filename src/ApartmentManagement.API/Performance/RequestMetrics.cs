using System.Diagnostics;
namespace ApartmentManagement.Performance;

// Theo dõi mỗi HTTP request: thời gian tổng (Stopwatch), số query DB và tổng ms thực thi SQL.
public sealed class RequestMetrics
{
    private long _dbQueryCount;
    private long _dbQueryElapsedMilliseconds;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // Reset đầu pipeline (ResponseTimingMiddleware) cho request mới.
    public void Reset()
    {
        Interlocked.Exchange(ref _dbQueryCount, 0);
        Interlocked.Exchange(ref _dbQueryElapsedMilliseconds, 0);
        _stopwatch.Restart();
    }

    public void IncrementDbQueryCount() => Interlocked.Increment(ref _dbQueryCount);

    public void AddDbQueryElapsedMilliseconds(long milliseconds) => Interlocked.Add(ref _dbQueryElapsedMilliseconds, milliseconds);

    public long DbQueryCount => Interlocked.Read(ref _dbQueryCount);
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    public long DbQueryElapsedMilliseconds => Interlocked.Read(ref _dbQueryElapsedMilliseconds);
}
