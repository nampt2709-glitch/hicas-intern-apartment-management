using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Services;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

public sealed class UtilityServiceService : CrudServiceBase<UtilityService, UtilityServiceReadDto, UtilityServiceCreateDto, UtilityServiceUpdateDto>, IUtilityServiceService
{
    private readonly IUtilityServiceRepository _repository;

    public UtilityServiceService(IMapper mapper, ICacheService cache, IUtilityServiceRepository repository)
        : base(mapper, cache, repository)
    {
        _repository = repository;
    }

    protected override IQueryable<UtilityService> BuildReadQuery(bool includeDeleted)
        => _repository.Query(true, includeDeleted);

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
