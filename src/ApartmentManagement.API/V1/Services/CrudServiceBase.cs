using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

public abstract class CrudServiceBase<TEntity, TReadDto, TCreateDto, TUpdateDto>
    where TEntity : BaseEntity, new()
{
    protected readonly IMapper Mapper;
    protected readonly ICacheService Cache;
    protected readonly IGenericRepository<TEntity> Repository;

    protected CrudServiceBase(IMapper mapper, ICacheService cache, IGenericRepository<TEntity> repository)
    {
        Mapper = mapper;
        Cache = cache;
        Repository = repository;
    }

    protected virtual IQueryable<TEntity> BuildReadQuery(bool includeDeleted)
        => Repository.Query(asNoTracking: true, includeDeleted: includeDeleted);

    protected virtual string CacheScope => typeof(TEntity).Name.ToLowerInvariant();

    protected virtual TimeSpan CacheDuration => TimeSpan.FromMinutes(5);

    protected virtual void PrepareForCreate(TEntity entity)
    {
    }

    protected virtual void PrepareForUpdate(TEntity entity, TUpdateDto dto)
    {
        Mapper.Map(dto, entity);
    }

    protected virtual IQueryable<TEntity> ApplySearchAndSort(IQueryable<TEntity> query, PaginationQueryDto paging)
        => query.OrderByDescending(x => x.CreatedAt);

    protected virtual async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        await Cache.InvalidateScopeAsync(CacheScope, cancellationToken);
        await Cache.InvalidateScopeAsync(CacheScope + ":single", cancellationToken);
        await Cache.InvalidateScopeAsync(CacheScope + ":tree", cancellationToken);
    }

    public virtual async Task<PagedResultDto<TReadDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"page={query.PageNumber};size={query.PageSize};search={query.Search};sort={query.SortBy};desc={query.Descending};deleted={query.IncludeDeleted}";
        return await Cache.GetOrCreateAsync(CacheScope, cacheKey, async ct =>
        {
            var baseQuery = ApplySearchAndSort(BuildReadQuery(query.IncludeDeleted), query);
            var total = await baseQuery.CountAsync(ct);
            var items = await baseQuery.Skip((query.PageNumber - 1) * query.PageSize)
                                       .Take(query.PageSize)
                                       .ToListAsync(ct);

            return items.Select(x => Mapper.Map<TReadDto>(x)).ToList()
                        .ToPagedResult(query.PageNumber, query.PageSize, total);
        }, CacheDuration, cancellationToken);
    }

    public virtual async Task<TReadDto> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        return await Cache.GetOrCreateAsync(CacheScope + ":single", id.ToString(), async ct =>
        {
            var entity = await BuildReadQuery(includeDeleted).FirstOrDefaultAsync(x => x.Id == id, ct)
                         ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found.");
            return Mapper.Map<TReadDto>(entity);
        }, CacheDuration, cancellationToken);
    }

    public virtual async Task<TReadDto> CreateAsync(TCreateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = Mapper.Map<TEntity>(dto);
        var utc = DateTime.UtcNow;
        entity.CreatedAt = utc;
        entity.UpdatedAt = null;
        PrepareForCreate(entity);
        await Repository.AddAsync(entity, cancellationToken);
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        return Mapper.Map<TReadDto>(entity);
    }

    public virtual async Task<TReadDto> UpdateAsync(Guid id, TUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, asNoTracking: false, includeDeleted: false, cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found.");

        PrepareForUpdate(entity, dto);
        entity.UpdatedAt = DateTime.UtcNow;
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        return Mapper.Map<TReadDto>(entity);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, asNoTracking: false, includeDeleted: false, cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found.");

        await Repository.SoftDeleteAsync(entity, cancellationToken);
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
    }

    public virtual async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, asNoTracking: false, includeDeleted: true, cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} not found.");

        if (!entity.IsDeleted)
            throw new InvalidOperationException($"{typeof(TEntity).Name} is not deleted.");

        await Repository.RestoreAsync(entity, cancellationToken);
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
    }
}
