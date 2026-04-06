// DTO đăng nhập bằng email và mật khẩu.
namespace ApartmentManagement.API.V1.DTOs.Auth;

public class LoginRequestDto
{
    // Email đăng nhập.
    public string Email { get; set; } = string.Empty;
    // Mật khẩu.
    public string Password { get; set; } = string.Empty;
}
