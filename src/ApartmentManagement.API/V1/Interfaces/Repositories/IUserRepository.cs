using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

// Truy vấn ApplicationUser và map vai trò theo lô (tránh N+1 khi phân trang).
public interface IUserRepository
{
    IQueryable<ApplicationUser> Query(bool asNoTracking = true, bool includeDeleted = false);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Một truy vấn: tên vai trò theo id người dùng (phân trang không N+1 GetRolesAsync).
    Task<Dictionary<Guid, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default);
}
