using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

public sealed class RefreshTokenRepository : RepositoryBase<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    public Task<RefreshToken?> GetActiveByHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Db.Set<RefreshToken>()
            .Include(x => x.User)
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Db.Set<RefreshToken>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
}
