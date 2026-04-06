// DTO thông tin người dùng hiện tại sau khi xác thực (hồ sơ + vai trò).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public class CurrentUserDto
{
    // Định danh người dùng.
    public Guid UserId { get; set; }
    // Email đăng nhập.
    public string Email { get; set; } = string.Empty;
    // Họ và tên.
    public string FullName { get; set; } = string.Empty;
    // Số điện thoại (có thể null).
    public string? PhoneNumber { get; set; }
    // Đường dẫn ảnh đại diện (nếu có).
    public string? AvatarPath { get; set; }
    // Danh sách tên vai trò trong hệ thống.
    public IList<string> Roles { get; set; } = new List<string>();
}
