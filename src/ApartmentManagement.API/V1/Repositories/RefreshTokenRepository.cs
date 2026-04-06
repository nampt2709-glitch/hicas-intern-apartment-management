using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

// Repository refresh token: tra cứu theo hash, kèm user khi làm mới phiên.
public sealed class RefreshTokenRepository : RepositoryBase<RefreshToken>, IRefreshTokenRepository
{
    // Khởi tạo với DbContext chung.
    public RefreshTokenRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    // Tìm refresh token còn hiệu lực (chưa revoke, chưa hết hạn) và Include navigation User.
    public Task<RefreshToken?> GetActiveByHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Db.Set<RefreshToken>()
            .Include(x => x.User)
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

    // Tìm bản ghi theo hash (dùng khi đăng xuất, không yêu cầu còn active).
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Db.Set<RefreshToken>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
}
