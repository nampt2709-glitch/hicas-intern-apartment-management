using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

// Refresh token: tìm theo hash (kèm user khi cần làm mới phiên).
public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetActiveByHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
