using ApartmentManagement.API.V1.DTOs.Auth;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
    Task<TokenPairDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default);
    Task<LogoutResultDto> LogoutAsync(Guid currentUserId, string? accessToken, string? refreshToken, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> MeAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default);
}
