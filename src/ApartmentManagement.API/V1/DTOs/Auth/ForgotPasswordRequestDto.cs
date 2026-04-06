// DTO yêu cầu gửi email quên mật khẩu (chỉ cần email).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class ForgotPasswordRequestDto
{
    // Email tài khoản cần khôi phục.
    public string Email { get; set; } = string.Empty;
}

