using Microsoft.Extensions.Caching.Memory;

namespace ApartmentManagement.Infrastructure;

/// <summary>
/// Simple fixed-window quota limiter for endpoint-specific rules that need
/// multi-dimensional keys (per IP + per account) or "failed-only" tracking.
/// In-memory: resets on app restart.
/// </summary>
public sealed class QuotaRateLimiter
{
    private readonly IMemoryCache _cache;

    public QuotaRateLimiter(IMemoryCache cache)
    {
        _cache = cache;
    }

    private sealed class WindowState
    {
        public DateTimeOffset WindowStart { get; init; }
        public int Count { get; set; }
    }

    public bool TryConsume(string key, int limit, TimeSpan window, out int remaining, out DateTimeOffset resetAt)
    {
        if (limit <= 0) limit = 1;
        if (window <= TimeSpan.Zero) window = TimeSpan.FromMinutes(1);

        var now = DateTimeOffset.UtcNow;
        var state = _cache.Get<WindowState>(key);
        if (state is null || now - state.WindowStart >= window)
        {
            state = new WindowState { WindowStart = now, Count = 0 };
        }

        resetAt = state.WindowStart + window;

        if (state.Count >= limit)
        {
            remaining = 0;
            _cache.Set(key, state, resetAt);
            return false;
        }

        state.Count++;
        remaining = Math.Max(0, limit - state.Count);
        _cache.Set(key, state, resetAt);
        return true;
    }

    /// <summary>
    /// Check quota without consuming a permit.
    /// </summary>
    public bool IsExceeded(string key, int limit, TimeSpan window, out int remaining, out DateTimeOffset resetAt)
    {
        if (limit <= 0) limit = 1;
        if (window <= TimeSpan.Zero) window = TimeSpan.FromMinutes(1);

        var now = DateTimeOffset.UtcNow;
        var state = _cache.Get<WindowState>(key);
        if (state is null || now - state.WindowStart >= window)
        {
            remaining = limit;
            resetAt = now + window;
            return false;
        }

        resetAt = state.WindowStart + window;
        remaining = Math.Max(0, limit - state.Count);
        return state.Count >= limit;
    }
}

