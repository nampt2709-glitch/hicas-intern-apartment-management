// DTO hoàn tất đặt lại mật khẩu qua email (token từ liên kết).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class ResetPasswordRequestDto
{
    // Email tài khoản cần đặt lại mật khẩu.
    public string Email { get; set; } = string.Empty;
    // Token xác thực gửi kèm email.
    public string Token { get; set; } = string.Empty;
    // Mật khẩu mới do người dùng chọn.
    public string NewPassword { get; set; } = string.Empty;
}

