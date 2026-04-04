using ApartmentManagement.API.V1.DTOs.Apartments;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

public sealed class ApartmentService : CrudServiceBase<Apartment, ApartmentReadDto, ApartmentCreateDto, ApartmentUpdateDto>, IApartmentService
{
    private readonly IApartmentRepository _repository;

    public ApartmentService(IMapper mapper, ICacheService cache, IApartmentRepository repository)
        : base(mapper, cache, repository)
    {
        _repository = repository;
    }

    protected override IQueryable<Apartment> BuildReadQuery(bool includeDeleted)
        => _repository.Query(true, includeDeleted);

    private static IQueryable<ApartmentReadDto> ProjectToReadDto(IQueryable<Apartment> query)
        => query.Select(a => new ApartmentReadDto
        {
            Id = a.Id,
            ApartmentNumber = a.ApartmentNumber,
            Floor = a.Floor,
            Area = a.Area,
            Status = a.Status,
            Description = a.Description,
            CoverImagePath = a.CoverImagePath,
            ResidentCount = a.Residents.Count(),
            InvoiceCount = a.Invoices.Count()
        });

    protected override IQueryable<Apartment> ApplySearchAndSort(IQueryable<Apartment> query, PaginationQueryDto paging)
    {
        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var search = paging.Search.Trim();
            var numberPattern = SqlLikePrefix.ForStartsWith(search);
            query = query.Where(x =>
                EF.Functions.Like(x.ApartmentNumber, numberPattern) ||
                (x.Description != null && x.Description.Contains(search)));
        }

        return paging.SortBy?.ToLowerInvariant() switch
        {
            "number" => paging.Descending ? query.OrderByDescending(x => x.ApartmentNumber) : query.OrderBy(x => x.ApartmentNumber),
            "floor" => paging.Descending ? query.OrderByDescending(x => x.Floor) : query.OrderBy(x => x.Floor),
            "area" => paging.Descending ? query.OrderByDescending(x => x.Area) : query.OrderBy(x => x.Area),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }

    public override async Task<PagedResultDto<ApartmentReadDto>> GetPagedAsync(
        PaginationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"page={query.PageNumber};size={query.PageSize};search={query.Search};sort={query.SortBy};desc={query.Descending};deleted={query.IncludeDeleted}";
        return await Cache.GetOrCreateAsync(CacheScope, cacheKey, async ct =>
        {
            var filtered = ApplySearchAndSort(BuildReadQuery(query.IncludeDeleted), query);
            var total = await filtered.CountAsync(ct);
            var items = await ProjectToReadDto(filtered)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);
            return items.ToPagedResult(query.PageNumber, query.PageSize, total);
        }, CacheDuration, cancellationToken);
    }

    public override async Task<ApartmentReadDto> GetByIdAsync(
        Guid id,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        return await Cache.GetOrCreateAsync(CacheScope + ":single", id.ToString(), async ct =>
        {
            return await ProjectToReadDto(BuildReadQuery(includeDeleted).Where(a => a.Id == id))
                       .FirstOrDefaultAsync(ct)
                   ?? throw new KeyNotFoundException($"{typeof(Apartment).Name} not found.");
        }, CacheDuration, cancellationToken);
    }

    public override async Task<ApartmentReadDto> CreateAsync(ApartmentCreateDto dto, CancellationToken cancellationToken = default)
    {
        var created = await base.CreateAsync(dto, cancellationToken);
        return await GetByIdAsync(created.Id, false, cancellationToken);
    }

    public override async Task<ApartmentReadDto> UpdateAsync(Guid id, ApartmentUpdateDto dto, CancellationToken cancellationToken = default)
    {
        _ = await base.UpdateAsync(id, dto, cancellationToken);
        return await GetByIdAsync(id, false, cancellationToken);
    }

    public async Task<ApartmentReadDto> GetMineForResidentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await ProjectToReadDto(
                BuildReadQuery(false).Where(a => a.Residents.Any(r => r.UserId == userId)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No apartment is linked to your account.");
    }

    public async Task AttachUploadedImageAsync(
        Guid apartmentId,
        string storedFilePath,
        string originalFileName,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var apartment = await _repository.GetTrackedByIdAsync(apartmentId, cancellationToken)
                        ?? throw new KeyNotFoundException("Apartment not found.");

        var utc = DateTime.UtcNow;
        if (apartment.CoverImagePath is null)
        {
            apartment.CoverImagePath = storedFilePath;
            apartment.UpdatedAt = utc;
        }

        var sortOrder = await _repository.CountImagesForApartmentAsync(apartmentId, cancellationToken) + 1;
        await _repository.AddApartmentImageAsync(new ApartmentImage
        {
            ApartmentId = apartmentId,
            FilePath = storedFilePath,
            OriginalFileName = originalFileName,
            MimeType = mimeType,
            SortOrder = sortOrder,
            CreatedAt = utc
        }, cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
    }
}
