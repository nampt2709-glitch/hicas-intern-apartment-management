using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Residents;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

public sealed class ResidentService : CrudServiceBase<Resident, ResidentReadDto, ResidentCreateDto, ResidentUpdateDto>, IResidentService
{
    private readonly IResidentRepository _repository;

    public ResidentService(IMapper mapper, ICacheService cache, IResidentRepository repository)
        : base(mapper, cache, repository)
    {
        _repository = repository;
    }

    protected override IQueryable<Resident> BuildReadQuery(bool includeDeleted)
        => _repository.Query(true, includeDeleted);

    protected override IQueryable<Resident> ApplySearchAndSort(IQueryable<Resident> query, PaginationQueryDto paging)
    {
        if (!string.IsNullOrWhiteSpace(paging.Search))
        {
            var search = paging.Search.Trim();
            var phonePattern = SqlLikePrefix.ForStartsWith(search);
            query = query.Where(x =>
                x.FullName.Contains(search) ||
                EF.Functions.Like(x.PhoneNumber, phonePattern) ||
                (x.Email != null && x.Email.Contains(search)));
        }

        return paging.SortBy?.ToLowerInvariant() switch
        {
            "name" => paging.Descending ? query.OrderByDescending(x => x.FullName) : query.OrderBy(x => x.FullName),
            "phone" => paging.Descending ? query.OrderByDescending(x => x.PhoneNumber) : query.OrderBy(x => x.PhoneNumber),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
    }

    public async Task<ResidentReadDto> GetMineForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await BuildReadQuery(false).FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
                     ?? throw new KeyNotFoundException("No resident profile is linked to your account.");
        return Mapper.Map<ResidentReadDto>(entity);
    }
}
