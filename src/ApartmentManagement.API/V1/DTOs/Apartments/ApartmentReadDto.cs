// DTO đọc thông tin căn hộ kèm ảnh bìa và số liệu thống kê liên quan.
using ApartmentManagement.API.V1.Entities.Enums;

namespace ApartmentManagement.API.V1.DTOs.Apartments;

public class ApartmentReadDto
{
    // Khóa chính căn hộ.
    public Guid Id { get; set; }
    // Số căn.
    public string ApartmentNumber { get; set; } = string.Empty;
    // Tầng.
    public int Floor { get; set; }
    // Diện tích.
    public decimal Area { get; set; }
    // Trạng thái hiện tại.
    public ApartmentStatus Status { get; set; }
    // Mô tả.
    public string? Description { get; set; }
    // Đường dẫn ảnh đại diện / bìa.
    public string? CoverImagePath { get; set; }
    // Số cư dân đang gắn với căn.
    public int ResidentCount { get; set; }
    // Số hóa đơn liên quan (hoặc trong kỳ tùy nghiệp vụ).
    public int InvoiceCount { get; set; }
}
