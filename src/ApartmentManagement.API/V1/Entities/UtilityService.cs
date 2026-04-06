namespace ApartmentManagement.API.V1.Entities;

// Danh mục dịch vụ tiện ích (đơn giá, đơn vị) dùng cho chi tiết hóa đơn; không có quan hệ điều hướng tới thực thể khác trong file này.
public class UtilityService : BaseEntity
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
