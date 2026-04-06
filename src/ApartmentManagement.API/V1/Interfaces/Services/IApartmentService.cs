using ApartmentManagement.API.V1.DTOs.Apartments;
using ApartmentManagement.API.V1.DTOs.Common;

namespace ApartmentManagement.API.V1.Interfaces.Services;

// Căn hộ: phân trang, căn “của tôi”, CRUD, khôi phục, gắn ảnh sau khi upload.
public interface IApartmentService
{
    Task<PagedResultDto<ApartmentReadDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> GetMineForResidentAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> CreateAsync(ApartmentCreateDto dto, CancellationToken cancellationToken = default);
    Task<ApartmentReadDto> UpdateAsync(Guid id, ApartmentUpdateDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    // Lưu bản ghi ảnh căn hộ và tùy chọn đặt ảnh bìa lần đầu sau khi file đã lưu trên đĩa.
    Task AttachUploadedImageAsync(Guid apartmentId, string storedFilePath, string originalFileName, string mimeType, CancellationToken cancellationToken = default);
}
