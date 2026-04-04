using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetActiveByHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
