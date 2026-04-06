// DTO kết quả đăng nhập thành công: thông tin user + cặp token.
namespace ApartmentManagement.API.V1.DTOs.Auth;

public class AuthResultDto
{
    // Mã người dùng.
    public Guid UserId { get; set; }
    // Email.
    public string Email { get; set; } = string.Empty;
    // Họ tên.
    public string FullName { get; set; } = string.Empty;
    // Vai trò được gán.
    public IList<string> Roles { get; set; } = new List<string>();
    // Access token và refresh token.
    public TokenPairDto Tokens { get; set; } = new();
}
