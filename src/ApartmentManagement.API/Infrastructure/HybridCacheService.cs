using System.Collections.Concurrent;
using System.Text.Json;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.Performance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApartmentManagement.Infrastructure;

// Cache lai: Redis (nếu cấu hình) với version theo scope + fallback IMemoryCache; gắn CacheInfo cho header chẩn đoán.
public sealed class HybridCacheService : ICacheService, IDisposable
{
    private static readonly TimeSpan RedisRetryDelay = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly string? _redisConnectionString;
    private readonly SemaphoreSlim _redisInitLock = new(1, 1);
    private readonly ConcurrentDictionary<string, long> _localVersions = new(StringComparer.OrdinalIgnoreCase);

    private volatile ConnectionMultiplexer? _redis;
    private long _nextRedisProbeAtUnixMs = 0;

    public HybridCacheService(IMemoryCache memoryCache, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, ILogger<HybridCacheService> logger)
    {
        _memoryCache = memoryCache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _redisConnectionString = configuration["Redis:ConnectionString"] ?? configuration.GetConnectionString("Redis");
    }

    public async Task<T> GetOrCreateAsync<T>(string scopeKey, string cacheKey, Func<CancellationToken, Task<T>> factory, TimeSpan absoluteExpirationRelativeToNow, CancellationToken cancellationToken = default)
    {
        // Key cache gồm version theo scope — InvalidateScope chỉ cần tăng version (không xóa từng key).
        var redis = await GetRedisAsync(cancellationToken);
        if (redis is not null)
        {
            var version = await GetScopeVersionAsync(redis, scopeKey);
            var versionedKey = BuildVersionedKey(scopeKey, cacheKey, version);
            var database = redis.GetDatabase();

            var cached = await database.StringGetAsync(versionedKey);
            if (cached.HasValue)
            {
                SetCacheInfo(CacheInfoDto.RedisHit);
                return Deserialize<T>(cached!);
            }

            var created = await factory(cancellationToken);
            await database.StringSetAsync(versionedKey, Serialize(created), absoluteExpirationRelativeToNow);
            SetCacheInfo(CacheInfoDto.RedisMiss);
            return created;
        }

        // Version scope lưu trong ConcurrentDictionary khi không dùng Redis.
        var localVersion = _localVersions.TryGetValue(scopeKey, out var currentVersion) ? currentVersion : 0;
        var localKey = BuildVersionedKey(scopeKey, cacheKey, localVersion);

        if (_memoryCache.TryGetValue(localKey, out T? memoryValue) && memoryValue is not null)
        {
            SetCacheInfo(CacheInfoDto.InMemoryHit);
            return memoryValue;
        }

        var result = await factory(cancellationToken);
        _memoryCache.Set(localKey, result, absoluteExpirationRelativeToNow);
        SetCacheInfo(CacheInfoDto.InMemoryMiss);
        return result;
    }

    public async Task InvalidateScopeAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        // Tăng version Redis hoặc local để vô hiệu toàn bộ key trong scope.
        var redis = await GetRedisAsync(cancellationToken);
        if (redis is not null)
        {
            await redis.GetDatabase().StringIncrementAsync(BuildScopeVersionKey(scopeKey));
            return;
        }

        _localVersions.AddOrUpdate(scopeKey, 1, (_, current) => current + 1);
    }

    public async Task InvalidateScopesAsync(IEnumerable<string> scopeKeys, CancellationToken cancellationToken = default)
    {
        foreach (var scopeKey in scopeKeys.Distinct(StringComparer.OrdinalIgnoreCase))
            await InvalidateScopeAsync(scopeKey, cancellationToken);
    }

    public Task<CacheInfoDto> GetCurrentCacheInfoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(GetCurrentCacheInfo());

    public CacheInfoDto GetCurrentCacheInfo()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(CacheDiagnosticsKeys.HttpContextItemKey, out var value) == true && value is CacheInfoDto cacheInfo)
            return cacheInfo;

        return CacheInfoDto.None;
    }

    private async Task<ConnectionMultiplexer?> GetRedisAsync(CancellationToken cancellationToken)
    {
        // Không có connection string thì bỏ qua Redis; có thể backoff sau lỗi kết nối.
        if (string.IsNullOrWhiteSpace(_redisConnectionString))
        {
            SetCacheInfo(CacheInfoDto.None);
            return null;
        }

        var current = _redis;
        if (current is { IsConnected: true })
            return current;

        if (!ShouldProbeRedisAgain())
        {
            SetCacheInfo(CacheInfoDto.None);
            return null;
        }

        // Double-check locking: một luồng kết nối Redis, các luồng khác chờ semaphore.
        await _redisInitLock.WaitAsync(cancellationToken);
        try
        {
            current = _redis;
            if (current is { IsConnected: true })
                return current;

            if (!ShouldProbeRedisAgain())
            {
                SetCacheInfo(CacheInfoDto.None);
                return null;
            }

            try
            {
                // Timeout ngắn — fail nhanh sang memory; lên lịch probe lại sau RedisRetryDelay.
                var options = ConfigurationOptions.Parse(_redisConnectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 0;
                options.ConnectTimeout = 750;
                options.SyncTimeout = 750;
                options.KeepAlive = 30;

                current = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
                if (!current.IsConnected)
                {
                    _logger.LogWarning("Redis connected object is not connected. Falling back to memory.");
                    ScheduleNextRedisProbe();
                    current.Dispose();
                    SetCacheInfo(CacheInfoDto.None);
                    return null;
                }

                _redis = current;
                ClearRedisProbeDelay();
                return current;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis unavailable, using memory cache.");
                ScheduleNextRedisProbe();
                SetCacheInfo(CacheInfoDto.None);
                return null;
            }
        }
        finally
        {
            _redisInitLock.Release();
        }
    }

    private bool ShouldProbeRedisAgain() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= Interlocked.Read(ref _nextRedisProbeAtUnixMs);

    private void ScheduleNextRedisProbe() => Interlocked.Exchange(ref _nextRedisProbeAtUnixMs, DateTimeOffset.UtcNow.Add(RedisRetryDelay).ToUnixTimeMilliseconds());

    private void ClearRedisProbeDelay() => Interlocked.Exchange(ref _nextRedisProbeAtUnixMs, 0);

    private static async Task<long> GetScopeVersionAsync(ConnectionMultiplexer redis, string scopeKey)
    {
        var versionValue = await redis.GetDatabase().StringGetAsync(BuildScopeVersionKey(scopeKey)).ConfigureAwait(false);
        return versionValue.HasValue && long.TryParse(versionValue.ToString(), out var parsedVersion) ? parsedVersion : 0;
    }

    private static string BuildScopeVersionKey(string scopeKey) => $"cache:version:{scopeKey}";
    private static string BuildVersionedKey(string scopeKey, string cacheKey, long version) => $"cache:{scopeKey}:v{version}:{cacheKey}";
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T Deserialize<T>(RedisValue value) => JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions)!;

    // Ghi nhớ hit/miss cache vào HttpContext.Items để middleware/header đọc được.
    private void SetCacheInfo(CacheInfoDto cacheInfo)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        httpContext.Items[CacheDiagnosticsKeys.HttpContextItemKey] = cacheInfo;
    }

    // Giải phóng multiplexer Redis và khóa khởi tạo.
    public void Dispose()
    {
        _redis?.Dispose();
        _redisInitLock.Dispose();
    }
}
