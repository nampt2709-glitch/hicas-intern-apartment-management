// DTO cập nhật dịch vụ tiện ích (điện, nước, v.v.).
namespace ApartmentManagement.API.V1.DTOs.Services;

public class UtilityServiceUpdateDto
{
    // Tên dịch vụ hiển thị.
    public string ServiceName { get; set; } = string.Empty;
    // Đơn giá cho một đơn vị tính.
    public decimal UnitPrice { get; set; }
    // Đơn vị tính (kWh, m³, tháng...).
    public string Unit { get; set; } = string.Empty;
    // Còn áp dụng hay đã ngừng.
    public bool IsActive { get; set; } = true;
}
