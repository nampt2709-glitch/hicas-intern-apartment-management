// DTO đặt lại mật khẩu người dùng (thao tác quản trị).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class ResetUserPasswordDto
{
    // Mật khẩu mới sẽ gán cho tài khoản.
    public string NewPassword { get; set; } = string.Empty;
}

