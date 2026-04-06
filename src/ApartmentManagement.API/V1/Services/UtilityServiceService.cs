using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Services;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

// Dịch vụ quản lý dịch vụ tiện ích (điện, nước, v.v.) với tìm kiếm và sắp xếp tùy chỉnh.
public sealed class UtilityServiceService : CrudServiceBase<UtilityService, UtilityServiceReadDto, UtilityServiceCreateDto, UtilityServiceUpdateDto>, IUtilityServiceService
{
    private readonly IUtilityServiceRepository _repository;

    // Khởi tạo với repository chuyên biệt cho <see cref="UtilityService"/>.
    public UtilityServiceService(IMapper mapper, ICacheService cache, IUtilityServiceRepository repository)
        : base(mapper, cache, repository)
    {
        _repository = repository;
    }

    // Dùng truy vấn từ repository tiện ích.
    protected override IQueryable<UtilityService> BuildReadQuery(bool includeDeleted)
        => _repository.Query(true, includeDeleted);

    // Lọc theo tên/đơn vị và sắp theo name, price hoặc ngày tạo.
    protected override IQueryable<UtilityService> ApplySearchAndSort(IQueryable<UtilityService> query, PaginationQueryDto paging)
    {
        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var search = paging.Search.Trim();
            var namePattern = SqlLikePrefix.ForStartsWith(search);
            query = query.Where(x =>
                EF.Functions.Like(x.ServiceName, namePattern) ||
                x.Unit.Contains(search));
        }

        return paging.SortBy?.ToLowerInvariant() switch
        {
            "name" => paging.Descending ? query.OrderByDescending(x => x.ServiceName) : query.OrderBy(x => x.ServiceName),
            "price" => paging.Descending ? query.OrderByDescending(x => x.UnitPrice) : query.OrderBy(x => x.UnitPrice),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }
}
