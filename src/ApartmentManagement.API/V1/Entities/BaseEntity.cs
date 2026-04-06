using ApartmentManagement.API.V1.Entities.Abstractions;

namespace ApartmentManagement.API.V1.Entities;

// Lớp cơ sở cho hầu hết thực thể nghiệp vụ: khóa chính, audit và xoá mềm (không có navigation EF ở đây).
public abstract class BaseEntity : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
