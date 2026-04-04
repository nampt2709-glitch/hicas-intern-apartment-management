using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Residents;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface IResidentService
{
    Task<PagedResultDto<ResidentReadDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default);
    Task<ResidentReadDto> GetMineForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ResidentReadDto> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<ResidentReadDto> CreateAsync(ResidentCreateDto dto, CancellationToken cancellationToken = default);
    Task<ResidentReadDto> UpdateAsync(Guid id, ResidentUpdateDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
