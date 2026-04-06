// DTO đọc thông tin dịch vụ tiện ích (trả về API).
namespace ApartmentManagement.API.V1.DTOs.Services;

public class UtilityServiceReadDto
{
    // Khóa chính dịch vụ.
    public Guid Id { get; set; }
    // Tên dịch vụ.
    public string ServiceName { get; set; } = string.Empty;
    // Đơn giá.
    public decimal UnitPrice { get; set; }
    // Đơn vị tính.
    public string Unit { get; set; } = string.Empty;
    // Trạng thái hoạt động.
    public bool IsActive { get; set; }
}
