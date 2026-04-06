// DTO kết quả thao tác đăng xuất (ghi nhận server đã xử lý).
namespace ApartmentManagement.API.V1.DTOs.Auth;

public class LogoutResultDto
{
    // True nếu đăng xuất đã được ghi nhận.
    public bool LoggedOut { get; set; }
}
