// DTO tạo mới bản ghi cư dân cho một căn hộ.
namespace ApartmentManagement.API.V1.DTOs.Residents;

public class ResidentCreateDto
{
    // Căn hộ cần gán cư dân.
    public Guid ApartmentId { get; set; }
    // Liên kết tài khoản đăng nhập (tùy chọn).
    public Guid? UserId { get; set; }
    // Họ và tên đầy đủ.
    public string FullName { get; set; } = string.Empty;
    // Số điện thoại.
    public string PhoneNumber { get; set; } = string.Empty;
    // Email (tùy chọn).
    public string? Email { get; set; }
    // Đánh dấu chủ hộ nếu đúng.
    public bool IsOwner { get; set; }
    // Ngày bắt đầu ở (mặc định UTC hiện tại).
    public DateTime MoveInDate { get; set; } = DateTime.UtcNow;
}
