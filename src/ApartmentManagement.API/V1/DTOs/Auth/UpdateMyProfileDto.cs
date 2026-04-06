// DTO cập nhật hồ sơ cá nhân (không đổi email, vai trò hay trường chỉ admin).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class UpdateMyProfileDto
{
    // Họ và tên hiển thị.
    public string FullName { get; set; } = string.Empty;
    // Số điện thoại (tùy chọn).
    public string? PhoneNumber { get; set; }
    // Mật khẩu hiện tại (khi đổi mật khẩu).
    public string? CurrentPassword { get; set; }
    // Mật khẩu mới (khi đổi mật khẩu).
    public string? NewPassword { get; set; }
}
