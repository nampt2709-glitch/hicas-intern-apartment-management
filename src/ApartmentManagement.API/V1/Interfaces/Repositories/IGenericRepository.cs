using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

public interface IGenericRepository<T> where T : BaseEntity
{
    IQueryable<T> Query(bool asNoTracking = true, bool includeDeleted = false);
    Task<T?> GetByIdAsync(Guid id, bool asNoTracking = true, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task RestoreAsync(T entity, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
