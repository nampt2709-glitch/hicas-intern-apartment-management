// DTO đăng ký tài khoản tự phục vụ (người dùng mới).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class RegisterRequestDto
{
    // Email dùng làm tên đăng nhập.
    public string Email { get; set; } = string.Empty;
    // Mật khẩu do người dùng chọn.
    public string Password { get; set; } = string.Empty;
    // Họ và tên.
    public string FullName { get; set; } = string.Empty;
    // Số điện thoại liên hệ (có thể để trống).
    public string? PhoneNumber { get; set; }
}
