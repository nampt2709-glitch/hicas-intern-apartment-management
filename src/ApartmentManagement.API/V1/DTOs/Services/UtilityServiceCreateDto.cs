// DTO tạo mới dịch vụ tiện ích.
namespace ApartmentManagement.API.V1.DTOs.Services;

public class UtilityServiceCreateDto
{
    // Tên dịch vụ.
    public string ServiceName { get; set; } = string.Empty;
    // Đơn giá ban đầu.
    public decimal UnitPrice { get; set; }
    // Đơn vị tính.
    public string Unit { get; set; } = string.Empty;
    // Mặc định đang hoạt động.
    public bool IsActive { get; set; } = true;
}
