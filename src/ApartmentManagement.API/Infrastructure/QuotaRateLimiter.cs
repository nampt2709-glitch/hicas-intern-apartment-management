using Microsoft.Extensions.Caching.Memory;

namespace ApartmentManagement.Infrastructure;

// Giới hạn quota cửa sổ cố định cho quy tắc cần khóa đa chiều (IP + tài khoản) hoặc chỉ đếm lỗi. Reset khi restart process.
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

    // Hết cửa sổ hoặc chưa có state thì mở cửa sổ mới; tăng Count và lưu cache đến resetAt.
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

    // Kiểm tra đã vượt quota hay chưa mà không trừ lượt.
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

