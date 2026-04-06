using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Entities.Security;

// Thực thể lưu refresh token đã băm, gắn với người dùng và vòng đời thu hồi/thay thế.
public class RefreshToken : BaseEntity
{
    // Khóa ngoại tới người dùng sở hữu token.
    public Guid UserId { get; set; }
    // Điều hướng: người dùng Identity tương ứng (một-nhiều: user có nhiều refresh token theo thời gian).
    public ApplicationUser User { get; set; } = null!;

    // Giá trị băm của refresh token (không lưu plaintext).
    public string TokenHash { get; set; } = string.Empty;
    // Mã định danh JWT (jti) gắn với phiên access token.
    public string JwtId { get; set; } = string.Empty;

    // Thời điểm hết hạn token.
    public DateTime ExpiresAt { get; set; }
    // Thời điểm thu hồi (đăng xuất / xoay token).
    public DateTime? RevokedAt { get; set; }
    // Hash của token thay thế khi xoay chuỗi refresh.
    public string? ReplacedByTokenHash { get; set; }
}
