// DTO yêu cầu tạo người dùng mới (thường dùng cho quản trị viên).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class CreateUserRequestDto
{
    // Địa chỉ email đăng nhập.
    public string Email { get; set; } = string.Empty;
    // Mật khẩu ban đầu.
    public string Password { get; set; } = string.Empty;
    // Họ và tên hiển thị.
    public string FullName { get; set; } = string.Empty;
    // Số điện thoại (tùy chọn).
    public string? PhoneNumber { get; set; }
    // Danh sách tên vai trò gán cho người dùng.
    public IList<string> Roles { get; set; } = new List<string>();
}
