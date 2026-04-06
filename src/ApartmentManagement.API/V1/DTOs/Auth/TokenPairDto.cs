// DTO cặp token JWT (access + refresh) và thời điểm hết hạn.
namespace ApartmentManagement.API.V1.DTOs.Auth;

public class TokenPairDto
{
    // Access token JWT dùng gọi API.
    public string AccessToken { get; set; } = string.Empty;
    // Refresh token để cấp access token mới.
    public string RefreshToken { get; set; } = string.Empty;
    // Thời điểm hết hạn access token.
    public DateTime AccessTokenExpiresAt { get; set; }
    // Thời điểm hết hạn refresh token.
    public DateTime RefreshTokenExpiresAt { get; set; }
}
