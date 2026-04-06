// DTO tạo một dòng chi tiết khi lập/cập nhật hóa đơn.
namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceDetailCreateDto
{
    // Dịch vụ được tính tiền.
    public Guid UtilityServiceId { get; set; }
    // Số lượng sử dụng.
    public decimal Quantity { get; set; }
    // Đơn giá áp dụng.
    public decimal UnitPrice { get; set; }
    // Ghi chú (tùy chọn).
    public string? Note { get; set; }
}
