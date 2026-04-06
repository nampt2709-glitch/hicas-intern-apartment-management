using ApartmentManagement.API.V1.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ApartmentManagement.Infrastructure;

// Danh sách thu hồi token trong IMemoryCache (logout / revoke) — kiểm tra nhanh trong JwtBearer OnTokenValidated.
public sealed class TokenRevocationService : ITokenRevocationService
{
    private readonly IMemoryCache _cache;
    private const string AccessPrefix = "revoked:access:";
    private const string RefreshPrefix = "revoked:refresh:";

    public TokenRevocationService(IMemoryCache cache)
    {
        _cache = cache;
    }

    // lưu cờ revoked với TTL ngắn (trùng với vòng đời access token thực tế).
    public Task RevokeAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _cache.Set(AccessPrefix + accessToken, true, TimeSpan.FromHours(1));
        }

        return Task.CompletedTask;
    }

    // refresh token lưu TTL dài hơn (đồng bộ với refresh window).
    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            _cache.Set(RefreshPrefix + refreshToken, true, TimeSpan.FromDays(30));
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsAccessTokenRevokedAsync(string accessToken, CancellationToken cancellationToken = default)
        => Task.FromResult(_cache.TryGetValue(AccessPrefix + accessToken, out _));

    public Task<bool> IsRefreshTokenRevokedAsync(string refreshToken, CancellationToken cancellationToken = default)
        => Task.FromResult(_cache.TryGetValue(RefreshPrefix + refreshToken, out _));
}
