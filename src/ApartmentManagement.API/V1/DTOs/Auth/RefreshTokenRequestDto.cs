// DTO làm mới phiên đăng nhập bằng cặp token hiện có.
namespace ApartmentManagement.API.V1.DTOs.Auth;

public class RefreshTokenRequestDto
{
    // Access token (có thể hết hạn nhưng vẫn dùng để xác định phiên).
    public string AccessToken { get; set; } = string.Empty;
    // Refresh token hợp lệ kèm theo.
    public string RefreshToken { get; set; } = string.Empty;
}
