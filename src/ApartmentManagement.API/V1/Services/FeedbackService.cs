using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Feedbacks;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

// Dịch vụ phản hồi: phân trang theo vai trò, cây hội thoại, tạo và kiểm tra quyền tham chiếu căn hộ/hóa đơn.
public sealed class FeedbackService
    : CrudServiceBase<Feedback, FeedbackReadDto, FeedbackCreateDto, FeedbackCreateDto>,
      IFeedbackService
{
    private readonly IFeedbackRepository _feedbackRepository;
    private readonly IResidentRepository _residentRepository;
    private readonly IInvoiceRepository _invoiceRepository;

    // Khởi tạo với repository phản hồi, cư dân và hóa đơn (kiểm tra tham chiếu).
    public FeedbackService(
        IMapper mapper,
        ICacheService cache,
        IFeedbackRepository feedbackRepository,
        IResidentRepository residentRepository,
        IInvoiceRepository invoiceRepository)
        : base(mapper, cache, feedbackRepository)
    {
        _feedbackRepository = feedbackRepository;
        _residentRepository = residentRepository;
        _invoiceRepository = invoiceRepository;
    }

    // Truy vấn phản hồi kèm thông tin người dùng.
    protected override IQueryable<Feedback> BuildReadQuery(bool includeDeleted)
        => _feedbackRepository.Query(asNoTracking: true, includeDeleted: includeDeleted)
            .Include(x => x.User);

    // Lọc nội dung và sắp theo path hoặc thời gian tạo.
    protected override IQueryable<Feedback> ApplySearchAndSort(IQueryable<Feedback> query, PaginationQueryDto paging)
    {
        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var search = paging.Search.Trim();
            query = query.Where(x => x.Content.Contains(search));
        }

        return paging.SortBy?.ToLowerInvariant() switch
        {
            "path" => paging.Descending ? query.OrderByDescending(x => x.Path) : query.OrderBy(x => x.Path),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }

    // Phân trang phản hồi; user thường chỉ thấy gốc của chính họ, admin thấy theo bộ lọc.
    public async Task<PagedResultDto<FeedbackReadDto>> GetPagedAsync(
        PaginationQueryDto query,
        Guid actingUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"page={query.PageNumber};size={query.PageSize};search={query.Search};sort={query.SortBy};desc={query.Descending};deleted={query.IncludeDeleted};actor={actingUserId:N};admin={isAdmin}";
        return await Cache.GetOrCreateAsync(CacheScope, cacheKey, async ct =>
        {
            var baseQuery = ApplySearchAndSort(BuildReadQuery(query.IncludeDeleted), query);
            if (!isAdmin)
                baseQuery = baseQuery.Where(x => x.ParentFeedbackId == null && x.UserId == actingUserId);

            var total = await baseQuery.CountAsync(ct);
            var items = await baseQuery.Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);

            return items.Select(x => Mapper.Map<FeedbackReadDto>(x)).ToList()
                        .ToPagedResult(query.PageNumber, query.PageSize, total);
        }, CacheDuration, cancellationToken);
    }

    // Phân trang các bài đăng của một user cụ thể.
    public async Task<PagedResultDto<FeedbackReadDto>> GetMyPostsPagedAsync(
        PaginationQueryDto query,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"myposts={userId:N};page={query.PageNumber};size={query.PageSize};search={query.Search};sort={query.SortBy};desc={query.Descending};deleted={query.IncludeDeleted}";
        return await Cache.GetOrCreateAsync(CacheScope, cacheKey, async ct =>
        {
            var baseQuery = ApplySearchAndSort(BuildReadQuery(query.IncludeDeleted), query)
                .Where(x => x.UserId == userId);
            var total = await baseQuery.CountAsync(ct);
            var items = await baseQuery.Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct);
            return items.Select(x => Mapper.Map<FeedbackReadDto>(x)).ToList()
                .ToPagedResult(query.PageNumber, query.PageSize, total);
        }, CacheDuration, cancellationToken);
    }

    // Tạo phản hồi hoặc trả lời; kiểm tra quyền tham chiếu và quyền trong luồng hội thoại.
    public async Task<FeedbackReadDto> CreateAsync(
        FeedbackCreateDto dto,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!isAdmin)
        {
            if (dto.ReferenceApartmentId.HasValue)
            {
                var okApt = await _residentRepository.ExistsForUserAndApartmentAsync(userId, dto.ReferenceApartmentId.Value, cancellationToken);
                if (!okApt)
                    throw new UnauthorizedAccessException("You cannot reference that apartment.");
            }

            if (dto.ReferenceInvoiceId.HasValue)
            {
                var apartmentId = await _invoiceRepository.GetApartmentIdByInvoiceIdAsync(dto.ReferenceInvoiceId.Value, cancellationToken)
                    ?? throw new KeyNotFoundException("Invoice not found.");
                var okInv = await _residentRepository.ExistsForUserAndApartmentAsync(userId, apartmentId, cancellationToken);
                if (!okInv)
                    throw new UnauthorizedAccessException("You cannot reference that invoice.");
            }
        }

        var entity = Mapper.Map<Feedback>(dto);
        entity.UserId = userId;

        if (entity.ParentFeedbackId.HasValue)
        {
            var parent = await Repository.GetByIdAsync(entity.ParentFeedbackId.Value, asNoTracking: true, includeDeleted: false, cancellationToken)
                         ?? throw new KeyNotFoundException("Parent feedback not found.");

            if (!isAdmin)
            {
                var root = await GetThreadRootAsync(parent.Id, cancellationToken);
                if (root.UserId != userId)
                    throw new UnauthorizedAccessException("You cannot reply in this thread.");
            }

            entity.Path = $"{parent.Path}/{entity.Id:N}";
        }
        else
        {
            entity.Path = $"/{entity.Id:N}";
        }

        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = null;

        await Repository.AddAsync(entity, cancellationToken);
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        var withUser = await _feedbackRepository.Query(asNoTracking: true, includeDeleted: false)
            .Include(x => x.User)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);
        return Mapper.Map<FeedbackReadDto>(withUser);
    }

    // Lấy cây phản hồi (cache); user thường chỉ được xem luồng của mình khi chỉ định gốc.
    public Task<FeedbackTreeNodeDto> GetTreeAsync(
        Guid? rootFeedbackId,
        Guid actingUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!isAdmin && rootFeedbackId.HasValue)
            return GetTreeWithAccessCheckAsync(rootFeedbackId.Value, actingUserId, cancellationToken);

        var cacheKey = $"{(isAdmin ? "admin" : actingUserId.ToString("N"))}:{rootFeedbackId?.ToString() ?? "root"}";
        return Cache.GetOrCreateAsync(
            CacheScope + ":tree",
            cacheKey,
            async ct =>
            {
                var rows = await LoadTreeRowsAsync(rootFeedbackId, actingUserId, isAdmin, ct);
                return BuildTreeFromRows(rows);
            },
            TimeSpan.FromMinutes(3),
            cancellationToken);
    }

    // Tải cây sau khi xác minh user là chủ luồng gốc.
    private async Task<FeedbackTreeNodeDto> GetTreeWithAccessCheckAsync(
        Guid rootFeedbackId,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var root = await GetThreadRootAsync(rootFeedbackId, cancellationToken);
        if (root.UserId != actingUserId)
            throw new UnauthorizedAccessException("You cannot access this feedback thread.");

        var cacheKey = $"{actingUserId:N}:scoped:{rootFeedbackId:N}";
        return await Cache.GetOrCreateAsync(
            CacheScope + ":tree",
            cacheKey,
            async ct =>
            {
                var rows = await LoadTreeRowsAsync(rootFeedbackId, actingUserId, isAdmin: true, ct);
                return BuildTreeFromRows(rows);
            },
            TimeSpan.FromMinutes(3),
            cancellationToken);
    }

    // Ghép danh sách phẳng từ DB thành cây nút con.
    private static FeedbackTreeNodeDto BuildTreeFromRows(List<FeedbackTreeRowDto> rows)
    {
        var lookup = rows.ToDictionary(x => x.Id, x => new FeedbackTreeNodeDto
        {
            Id = x.Id,
            ParentFeedbackId = x.ParentFeedbackId,
            UserId = x.UserId,
            Content = x.Content,
            ReferenceApartmentId = x.ReferenceApartmentId,
            ReferenceInvoiceId = x.ReferenceInvoiceId,
            Path = x.Path,
            Depth = x.Depth,
            CreatedAt = x.CreatedAt
        });

        var roots = new List<FeedbackTreeNodeDto>();

        foreach (var node in lookup.Values.OrderBy(x => x.Depth).ThenBy(x => x.CreatedAt))
        {
            if (node.ParentFeedbackId.HasValue && lookup.TryGetValue(node.ParentFeedbackId.Value, out var parent))
                parent.Replies.Add(node);
            else
                roots.Add(node);
        }

        if (roots.Count == 1)
            return roots[0];

        return new FeedbackTreeNodeDto
        {
            Id = Guid.Empty,
            Content = "ROOT",
            Path = "/",
            CreatedAt = DateTime.UtcNow,
            Replies = roots
        };
    }

    // Trả về danh sách phẳng các nút trong cây (có kiểm tra quyền tương tự GetTree).
    public async Task<IReadOnlyList<FeedbackFlatDto>> GetFlattenedAsync(
        Guid? rootFeedbackId,
        Guid actingUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!isAdmin && rootFeedbackId.HasValue)
        {
            var root = await GetThreadRootAsync(rootFeedbackId.Value, cancellationToken);
            if (root.UserId != actingUserId)
                throw new UnauthorizedAccessException("You cannot access this feedback thread.");
        }

        var rows = await LoadTreeRowsAsync(rootFeedbackId, actingUserId, isAdmin, cancellationToken);
        return rows.Select(x => new FeedbackFlatDto
        {
            Id = x.Id,
            ParentFeedbackId = x.ParentFeedbackId,
            Depth = x.Depth,
            Path = x.Path,
            Content = x.Content,
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    // Xóa mềm phản hồi; user chỉ xóa được bài của mình trừ khi là admin.
    public async Task DeleteAsync(Guid id, Guid actingUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, asNoTracking: false, includeDeleted: false, cancellationToken: cancellationToken)
                     ?? throw new KeyNotFoundException("Feedback not found.");

        if (!isAdmin && entity.UserId != actingUserId)
            throw new UnauthorizedAccessException("You can only delete your own feedback.");

        await Repository.SoftDeleteAsync(entity, cancellationToken);
        await Repository.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
    }

    // Tìm bản ghi gốc của luồng từ path phân cấp.
    private async Task<Feedback> GetThreadRootAsync(Guid feedbackId, CancellationToken cancellationToken)
    {
        var current = await Repository.GetByIdAsync(feedbackId, asNoTracking: true, includeDeleted: false, cancellationToken)
                      ?? throw new KeyNotFoundException("Feedback not found.");
        var rootId = ParseRootFeedbackIdFromPath(current.Path);
        if (rootId == current.Id)
            return current;

        return await Repository.GetByIdAsync(rootId, asNoTracking: true, includeDeleted: false, cancellationToken)
               ?? throw new KeyNotFoundException("Feedback not found.");
    }

    // Trích Id phản hồi gốc từ phần đầu chuỗi path.
    private static Guid ParseRootFeedbackIdFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Feedback path is missing.");
        var first = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null || !Guid.TryParseExact(first, "N", out var rootId))
            throw new InvalidOperationException("Feedback path is invalid.");
        return rootId;
    }

    // Gọi repository trả về các dòng CTE đã sắp theo path.
    private Task<List<FeedbackTreeRowDto>> LoadTreeRowsAsync(
        Guid? rootFeedbackId,
        Guid actingUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var filter = new FeedbackTreeFilter(
            AllRootThreads: !rootFeedbackId.HasValue,
            ScopeRootId: rootFeedbackId ?? Guid.Empty,
            RestrictToActingUserOnly: !isAdmin && !rootFeedbackId.HasValue,
            ActingUserId: actingUserId);

        return _feedbackRepository.GetFeedbackTreeRowsAsync(filter, cancellationToken);
    }
}
