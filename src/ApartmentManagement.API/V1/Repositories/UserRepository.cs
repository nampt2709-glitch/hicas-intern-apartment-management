using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

// Repository truy vấn người dùng Identity và vai trò theo danh sách Id.
public sealed class UserRepository : IUserRepository
{
    private readonly ApartmentDbContext _db;

    // Khởi tạo với DbContext ứng dụng.
    public UserRepository(ApartmentDbContext db)
    {
        _db = db;
    }

    // Truy vấn <see cref="ApplicationUser"/>; có thể bỏ qua global filter khi cần bản đã xóa.
    public IQueryable<ApplicationUser> Query(bool asNoTracking = true, bool includeDeleted = false)
    {
        IQueryable<ApplicationUser> query = _db.Set<ApplicationUser>();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return query;
    }

    // Lưu thay đổi xuống cơ sở dữ liệu.
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    // Lấy map userId → danh tên vai trò cho một tập Id (tránh N+1 khi phân trang).
    public async Task<Dictionary<Guid, List<string>>> GetRoleNamesByUserIdsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, List<string>>();

        var distinct = userIds.Distinct().ToList();
        var rows = await (
                from ur in _db.Set<IdentityUserRole<Guid>>()
                join r in _db.Set<IdentityRole<Guid>>() on ur.RoleId equals r.Id
                where distinct.Contains(ur.UserId)
                select new { ur.UserId, RoleName = r.Name ?? string.Empty })
            .ToListAsync(cancellationToken);

        return rows
            .Where(x => x.RoleName.Length > 0)
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).Distinct().ToList());
    }
}
