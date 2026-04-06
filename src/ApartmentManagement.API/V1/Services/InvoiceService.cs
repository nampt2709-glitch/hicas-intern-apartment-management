using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Invoices;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

// Dịch vụ hóa đơn: CRUD có Include chi tiết, tổng tiền và danh sách “của cư dân”.
public sealed class InvoiceService : CrudServiceBase<Invoice, InvoiceReadDto, InvoiceCreateDto, InvoiceUpdateDto>, IInvoiceService
{
    private readonly IInvoiceRepository _repository;

    // Khởi tạo với repository hóa đơn (graph đọc/ghi chi tiết).
    public InvoiceService(IMapper mapper, ICacheService cache, IInvoiceRepository repository)
        : base(mapper, cache, repository)
    {
        _repository = repository;
    }

    // Truy vấn kèm căn hộ, chi tiết và dịch vụ tiện ích (split query).
    protected override IQueryable<Invoice> BuildReadQuery(bool includeDeleted)
        => _repository.Query(true, includeDeleted)
            .AsSplitQuery()
            .Include(x => x.Apartment)
            .Include(x => x.InvoiceDetails)
            .ThenInclude(x => x.UtilityService);

    // Tìm theo tiêu đề/số căn và sắp theo title, due date hoặc ngày tạo.
    protected override IQueryable<Invoice> ApplySearchAndSort(IQueryable<Invoice> query, PaginationQueryDto paging)
    {
        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var search = paging.Search.Trim();
            var aptPattern = SqlLikePrefix.ForStartsWith(search);
            query = query.Where(x =>
                x.Title.Contains(search) ||
                EF.Functions.Like(x.Apartment.ApartmentNumber, aptPattern));
        }

        return paging.SortBy?.ToLowerInvariant() switch
        {
            "title" => paging.Descending ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "duedate" => paging.Descending ? query.OrderByDescending(x => x.DueDate) : query.OrderBy(x => x.DueDate),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }

    // Phân trang hóa đơn thuộc các căn hộ mà cư dân (user) đang liên kết.
    public async Task<PagedResultDto<InvoiceReadDto>> GetMineForResidentAsync(PaginationQueryDto query, Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"mine={userId:N};page={query.PageNumber};size={query.PageSize};search={query.Search};sort={query.SortBy};desc={query.Descending};deleted={query.IncludeDeleted}";
        return await Cache.GetOrCreateAsync(CacheScope, cacheKey, async ct =>
        {
            var scoped = BuildReadQuery(query.IncludeDeleted)
                .Where(x => x.Apartment.Residents.Any(r => r.UserId == userId));
            var baseQuery = ApplySearchAndSort(scoped, query);
            var total = await baseQuery.CountAsync(ct);
            var items = await baseQuery.Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);
            return items.Select(x => Mapper.Map<InvoiceReadDto>(x)).ToList()
                .ToPagedResult(query.PageNumber, query.PageSize, total);
        }, CacheDuration, cancellationToken);
    }

    // Tạo hóa đơn, tính dòng chi tiết và tổng tiền, trả về bản đọc đầy đủ graph.
    public override async Task<InvoiceReadDto> CreateAsync(InvoiceCreateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = Mapper.Map<Invoice>(dto);
        entity.PaidAt = entity.IsPaid ? DateTime.UtcNow : null;
        var utc = DateTime.UtcNow;
        entity.CreatedAt = utc;
        entity.UpdatedAt = null;
        entity.InvoiceDetails = dto.Details.Select(d => new InvoiceDetail
        {
            UtilityServiceId = d.UtilityServiceId,
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            SubTotal = d.Quantity * d.UnitPrice,
            Note = d.Note,
            CreatedAt = utc
        }).ToList();
        entity.TotalAmount = entity.InvoiceDetails.Sum(x => x.SubTotal);

        await Repository.AddAsync(entity, cancellationToken);
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        var loaded = await _repository.GetWithReadGraphAsync(entity.Id, cancellationToken);
        return Mapper.Map<InvoiceReadDto>(loaded);
    }

    // Cập nhật hóa đơn: thay toàn bộ chi tiết, tính lại tổng và trả graph đọc.
    public override async Task<InvoiceReadDto> UpdateAsync(Guid id, InvoiceUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetTrackedWithDetailsAsync(id, cancellationToken)
                     ?? throw new KeyNotFoundException("Invoice not found.");

        entity.ApartmentId = dto.ApartmentId;
        entity.Title = dto.Title;
        entity.BillingMonth = dto.BillingMonth;
        entity.IssueDate = dto.IssueDate;
        entity.DueDate = dto.DueDate;
        entity.IsPaid = dto.IsPaid;
        entity.PaidAt = dto.IsPaid ? (entity.PaidAt ?? DateTime.UtcNow) : null;

        _repository.RemoveAllInvoiceDetails(entity);
        var utc = DateTime.UtcNow;
        entity.InvoiceDetails = dto.Details.Select(detail => new InvoiceDetail
        {
            UtilityServiceId = detail.UtilityServiceId,
            Quantity = detail.Quantity,
            UnitPrice = detail.UnitPrice,
            SubTotal = detail.Quantity * detail.UnitPrice,
            Note = detail.Note,
            CreatedAt = utc
        }).ToList();

        entity.TotalAmount = entity.InvoiceDetails.Sum(x => x.SubTotal);
        entity.UpdatedAt = utc;
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        var loaded = await _repository.GetWithReadGraphAsync(entity.Id, cancellationToken);
        return Mapper.Map<InvoiceReadDto>(loaded);
    }
}
