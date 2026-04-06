// DTO cập nhật thông tin một căn hộ (số căn, tầng, diện tích, trạng thái).
using ApartmentManagement.API.V1.Entities.Enums;

namespace ApartmentManagement.API.V1.DTOs.Apartments;

public class ApartmentUpdateDto
{
    // Số hoặc mã căn hộ.
    public string ApartmentNumber { get; set; } = string.Empty;
    // Tầng (số nguyên).
    public int Floor { get; set; }
    // Diện tích sử dụng.
    public decimal Area { get; set; }
    // Trạng thái căn (trống, đang ở, bảo trì...).
    public ApartmentStatus Status { get; set; } = ApartmentStatus.Available;
    // Mô tả chi tiết (tùy chọn).
    public string? Description { get; set; }
}
