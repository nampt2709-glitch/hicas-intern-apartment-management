using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Auth;

namespace ApartmentManagement.API.V1.Interfaces.Services;

// Dịch vụ quản lý người dùng (CRUD, hồ sơ, đặt lại mật khẩu, xóa cứng).
public interface IUserService
{
    Task<CurrentUserDto> CreateAsync(CreateUserRequestDto dto, CancellationToken cancellationToken = default);
    Task<PagedResultDto<CurrentUserDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> UpdateAsync(Guid id, CurrentUserDto dto, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> UpdateMeAsync(Guid userId, UpdateMyProfileDto dto, CancellationToken cancellationToken = default);
    Task<AdminPasswordResetResultDto> AdminResetPasswordAsync(Guid userId, ResetUserPasswordDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    // Xóa cứng người dùng và dữ liệu phụ thuộc (refresh token, phản hồi, liên kết cư dân). Chỉ admin.
    Task PurgeUserAsync(Guid id, CancellationToken cancellationToken = default);
}
