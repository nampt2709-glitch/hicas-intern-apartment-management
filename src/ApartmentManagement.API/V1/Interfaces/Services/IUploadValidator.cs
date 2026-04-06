using ApartmentManagement.API.V1.DTOs.Uploads;
using Microsoft.AspNetCore.Http;

namespace ApartmentManagement.API.V1.Interfaces.Services;

// Kiểm tra upload và lưu file (avatar người dùng, ảnh căn hộ).
public interface IUploadValidator
{
    Task ValidateRequestAsync(IReadOnlyCollection<IFormFile> files, CancellationToken cancellationToken = default);
    Task ValidateImageAsync(IFormFile file, CancellationToken cancellationToken = default);

    // Avatar: thư mục avatars/{userId:N}/ (ghi đè file cũ trong thư mục đó).
    Task<UploadResultDto> SaveUserAvatarAsync(IFormFile file, Guid userId, CancellationToken cancellationToken = default);

    // Ảnh gallery căn hộ: apartmentImages/{apartmentId:N}/.
    Task<UploadResultDto> SaveApartmentImageAsync(IFormFile file, Guid apartmentId, CancellationToken cancellationToken = default);
}
