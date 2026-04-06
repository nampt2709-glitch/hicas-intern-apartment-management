using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Services;

namespace ApartmentManagement.API.V1.Interfaces.Services;

// Dịch vụ nghiệp vụ: tiện ích (điện, nước...) — phân trang, CRUD, khôi phục sau soft-delete.
public interface IUtilityServiceService
{
    Task<PagedResultDto<UtilityServiceReadDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default);
    Task<UtilityServiceReadDto> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<UtilityServiceReadDto> CreateAsync(UtilityServiceCreateDto dto, CancellationToken cancellationToken = default);
    Task<UtilityServiceReadDto> UpdateAsync(Guid id, UtilityServiceUpdateDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
