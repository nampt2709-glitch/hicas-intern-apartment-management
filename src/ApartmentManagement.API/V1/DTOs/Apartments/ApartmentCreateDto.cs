// DTO tạo căn hộ mới với thông tin cơ bản và trạng thái ban đầu.
using ApartmentManagement.API.V1.Entities.Enums;

namespace ApartmentManagement.API.V1.DTOs.Apartments;

public class ApartmentCreateDto
{
    // Số hoặc mã căn.
    public string ApartmentNumber { get; set; } = string.Empty;
    // Tầng.
    public int Floor { get; set; }
    // Diện tích.
    public decimal Area { get; set; }
    // Trạng thái khởi tạo (thường là trống).
    public ApartmentStatus Status { get; set; } = ApartmentStatus.Available;
    // Ghi chú mô tả (tùy chọn).
    public string? Description { get; set; }
}
