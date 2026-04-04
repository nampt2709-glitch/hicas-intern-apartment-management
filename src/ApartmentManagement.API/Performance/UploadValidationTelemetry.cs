using System.Threading;

namespace ApartmentManagement.Performance;

public sealed class UploadValidationTelemetry
{
    private long _attempts;
    private long _successes;
    private long _failures;

    public void RecordAttempt() => Interlocked.Increment(ref _attempts);
    public void RecordSuccess() => Interlocked.Increment(ref _successes);
    public void RecordFailure() => Interlocked.Increment(ref _failures);

    public (long Attempts, long Successes, long Failures, double FailureRate) Snapshot()
    {
        var attempts = Interlocked.Read(ref _attempts);
        var successes = Interlocked.Read(ref _successes);
        var failures = Interlocked.Read(ref _failures);
        var failureRate = attempts <= 0 ? 0d : (double)failures / attempts;
        return (attempts, successes, failures, failureRate);
    }
}
