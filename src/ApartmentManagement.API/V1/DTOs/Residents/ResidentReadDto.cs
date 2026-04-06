// DTO đọc thông tin cư dân (trả về danh sách/chi tiết).
namespace ApartmentManagement.API.V1.DTOs.Residents;

public class ResidentReadDto
{
    // Khóa chính bản ghi cư dân.
    public Guid Id { get; set; }
    // Mã căn hộ liên quan.
    public Guid ApartmentId { get; set; }
    // Người dùng hệ thống liên kết (nếu có).
    public Guid? UserId { get; set; }
    // Họ và tên.
    public string FullName { get; set; } = string.Empty;
    // Số điện thoại.
    public string PhoneNumber { get; set; } = string.Empty;
    // Email.
    public string? Email { get; set; }
    // Chủ hộ hay không.
    public bool IsOwner { get; set; }
    // Ngày vào ở.
    public DateTime MoveInDate { get; set; }
    // Ngày rời đi (nếu có).
    public DateTime? MoveOutDate { get; set; }
}
