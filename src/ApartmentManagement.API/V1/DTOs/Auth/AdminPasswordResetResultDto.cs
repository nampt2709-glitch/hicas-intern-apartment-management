// DTO kết quả đặt lại mật khẩu do quản trị viên thực hiện.
namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class AdminPasswordResetResultDto
{
    // True nếu đặt lại mật khẩu thành công.
    public bool Reset { get; init; }
}
