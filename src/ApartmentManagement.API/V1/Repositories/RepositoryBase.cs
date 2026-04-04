using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

public class RepositoryBase<TEntity> : IGenericRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly ApartmentDbContext Db;

    public RepositoryBase(ApartmentDbContext db)
    {
        Db = db;
    }

    public virtual IQueryable<TEntity> Query(bool asNoTracking = true, bool includeDeleted = false)
    {
        IQueryable<TEntity> query = Db.Set<TEntity>();
        if (asNoTracking)
            query = query.AsNoTracking();
        if (includeDeleted)
            query = query.IgnoreQueryFilters();
        return query;
    }

    public virtual Task<TEntity?> GetByIdAsync(Guid id, bool asNoTracking = true, bool includeDeleted = false, CancellationToken cancellationToken = default)
        => Query(asNoTracking, includeDeleted).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();

    public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Db.Set<TEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utc;
        entity.UpdatedAt = utc;
        return Task.CompletedTask;
    }

    public virtual Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.UpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Db.SaveChangesAsync(cancellationToken);
}
