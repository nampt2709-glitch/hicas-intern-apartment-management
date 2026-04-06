using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

// Repository generic cho thực thể kế thừa <see cref="BaseEntity"/>: truy vấn, thêm, xóa mềm, khôi phục.
public class RepositoryBase<TEntity> : IGenericRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly ApartmentDbContext Db;

    // Gắn DbContext để các lớp con dùng <see cref="Db"/>.
    public RepositoryBase(ApartmentDbContext db)
    {
        Db = db;
    }

    // Truy vấn DbSet; có thể bỏ qua filter soft-delete khi <paramref name="includeDeleted"/> là true.
    public virtual IQueryable<TEntity> Query(bool asNoTracking = true, bool includeDeleted = false)
    {
        IQueryable<TEntity> query = Db.Set<TEntity>();
        if (asNoTracking)
            query = query.AsNoTracking();
        if (includeDeleted)
            query = query.IgnoreQueryFilters();
        return query;
    }

    // Lấy một thực thể theo Id.
    public virtual Task<TEntity?> GetByIdAsync(Guid id, bool asNoTracking = true, bool includeDeleted = false, CancellationToken cancellationToken = default)
        => Query(asNoTracking, includeDeleted).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    // Thêm thực thể mới (chưa SaveChanges).
    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();

    // Thêm nhiều thực thể cùng lúc.
    public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    // Đánh dấu thực thể đã theo dõi là đã sửa đổi.
    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Db.Set<TEntity>().Update(entity);
        return Task.CompletedTask;
    }

    // Đặt cờ xóa mềm và thời điểm xóa/cập nhật.
    public virtual Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = utc;
        entity.UpdatedAt = utc;
        return Task.CompletedTask;
    }

    // Bỏ cờ xóa mềm và khôi phục thời điểm xóa.
    public virtual Task RestoreAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.UpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    // Lưu thay đổi xuống cơ sở dữ liệu.
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Db.SaveChangesAsync(cancellationToken);
}
