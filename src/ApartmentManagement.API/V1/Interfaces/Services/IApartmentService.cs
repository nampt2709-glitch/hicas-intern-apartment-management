using ApartmentManagement.API.V1.DTOs.Apartments;
using ApartmentManagement.API.V1.DTOs.Common;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface IApartmentService
{
    Task<PagedResultDto<ApartmentReadDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> GetMineForResidentAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> CreateAsync(ApartmentCreateDto dto, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> UpdateAsync(Guid id, ApartmentUpdateDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists apartment image row and optional first cover path after file was saved to disk.</summary>
    Task AttachUploadedImageAsync(Guid apartmentId, string storedFilePath, string originalFileName, string mimeType, CancellationToken cancellationToken = default);
}
