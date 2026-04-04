using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApartmentDbContext _db;

    public UserRepository(ApartmentDbContext db)
    {
        _db = db;
    }

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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

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
