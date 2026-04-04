namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface ITokenRevocationService
{
    Task RevokeAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> IsAccessTokenRevokedAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<bool> IsRefreshTokenRevokedAsync(string refreshToken, CancellationToken cancellationToken = default);
}
