using ApartmentManagement.API.V1.DTOs.Common;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string scopeKey, string cacheKey, Func<CancellationToken, Task<T>> factory, TimeSpan absoluteExpirationRelativeToNow, CancellationToken cancellationToken = default);
    Task InvalidateScopeAsync(string scopeKey, CancellationToken cancellationToken = default);
    Task InvalidateScopesAsync(IEnumerable<string> scopeKeys, CancellationToken cancellationToken = default);
    Task<CacheInfoDto> GetCurrentCacheInfoAsync(CancellationToken cancellationToken = default);
    CacheInfoDto GetCurrentCacheInfo();
}
