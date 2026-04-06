using Microsoft.AspNetCore.Identity;
using ApartmentManagement.API.V1.Entities.Abstractions;
using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Entities.Security;

// Người dùng ứng dụng (mở rộng Identity) với xoá mềm và các tập điều hướng nghiệp vụ.
public class ApplicationUser : IdentityUser<Guid>, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Điều hướng: tất cả refresh token còn lưu của user (một user — nhiều token theo thiết bị/phiên).
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    // Điều hướng: các phản hồi/ bài viết do user này tạo (một user — nhiều Feedback).
    public ICollection<Feedback> AuthoredFeedbacks { get; set; } = new List<Feedback>();
}
