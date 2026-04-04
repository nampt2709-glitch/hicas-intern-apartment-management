using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

public interface IUserRepository
{
    IQueryable<ApplicationUser> Query(bool asNoTracking = true, bool includeDeleted = false);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>One query: role names per user id (for paging without N+1 GetRolesAsync).</summary>
    Task<Dictionary<Guid, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default);
}
