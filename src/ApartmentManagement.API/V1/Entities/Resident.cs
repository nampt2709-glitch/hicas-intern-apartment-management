using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Entities;

// Người ở / cư dân gắn với một căn hộ và tùy chọn liên kết tài khoản đăng nhập.
public class Resident : BaseEntity
{
    public Guid ApartmentId { get; set; }
    // Điều hướng: căn hộ mà cư dân đang thuộc về (nhiều cư dân — một căn).
    public Apartment Apartment { get; set; } = null!;

    public Guid? UserId { get; set; }
    // Điều hướng: tài khoản ApplicationUser nếu cư dân có đăng nhập hệ thống (không bắt buộc).
    public ApplicationUser? Account { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsOwner { get; set; }
    public DateTime MoveInDate { get; set; } = DateTime.UtcNow;
    public DateTime? MoveOutDate { get; set; }
}
