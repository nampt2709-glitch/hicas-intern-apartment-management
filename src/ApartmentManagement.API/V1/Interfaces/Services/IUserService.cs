using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Auth;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface IUserService
{
    Task<CurrentUserDto> CreateAsync(CreateUserRequestDto dto, CancellationToken cancellationToken = default);
    Task<PagedResultDto<CurrentUserDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> UpdateAsync(Guid id, CurrentUserDto dto, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> UpdateMeAsync(Guid userId, UpdateMyProfileDto dto, CancellationToken cancellationToken = default);
    Task<AdminPasswordResetResultDto> AdminResetPasswordAsync(Guid userId, ResetUserPasswordDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Hard-delete user and dependent rows (refresh tokens, authored feedbacks, resident links). Admin-only.</summary>
    Task PurgeUserAsync(Guid id, CancellationToken cancellationToken = default);
}
