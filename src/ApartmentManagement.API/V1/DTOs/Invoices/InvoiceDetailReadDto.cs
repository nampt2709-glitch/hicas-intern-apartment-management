// DTO đọc một dòng chi tiết trên hóa đơn (dịch vụ + số tiền).
namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceDetailReadDto
{
    // Khóa chính dòng chi tiết.
    public Guid Id { get; set; }
    // Hóa đơn chứa dòng này.
    public Guid InvoiceId { get; set; }
    // Dịch vụ tiện ích áp dụng.
    public Guid UtilityServiceId { get; set; }
    // Tên dịch vụ (denormalize để hiển thị).
    public string ServiceName { get; set; } = string.Empty;
    // Số lượng (theo đơn vị dịch vụ).
    public decimal Quantity { get; set; }
    // Đơn giá tại thời điểm lập hóa đơn.
    public decimal UnitPrice { get; set; }
    // Thành tiền dòng (số lượng × đơn giá).
    public decimal SubTotal { get; set; }
    // Ghi chú thêm (tùy chọn).
    public string? Note { get; set; }
}
