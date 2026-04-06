// DTO cập nhật thông tin cư dân / người ở căn hộ.
namespace ApartmentManagement.API.V1.DTOs.Residents;

public class ResidentUpdateDto
{
    // Căn hộ mà cư dân đang gắn với.
    public Guid ApartmentId { get; set; }
    // Tài khoản người dùng liên kết (nếu có).
    public Guid? UserId { get; set; }
    // Họ và tên.
    public string FullName { get; set; } = string.Empty;
    // Số điện thoại.
    public string PhoneNumber { get; set; } = string.Empty;
    // Email liên hệ (tùy chọn).
    public string? Email { get; set; }
    // True nếu là chủ hộ / sở hữu.
    public bool IsOwner { get; set; }
    // Ngày bắt đầu ở.
    public DateTime MoveInDate { get; set; } = DateTime.UtcNow;
    // Ngày chuyển đi (nếu đã rời).
    public DateTime? MoveOutDate { get; set; }
}
