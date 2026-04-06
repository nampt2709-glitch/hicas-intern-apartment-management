// DTO tạo hóa đơn mới kèm danh sách dòng chi tiết.
namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceCreateDto
{
    // Căn hộ phát sinh hóa đơn.
    public Guid ApartmentId { get; set; }
    // Tiêu đề hoặc mô tả hóa đơn.
    public string Title { get; set; } = string.Empty;
    // Tháng kỳ cước.
    public DateOnly BillingMonth { get; set; }
    // Ngày phát hành.
    public DateTime IssueDate { get; set; }
    // Ngày đến hạn thanh toán.
    public DateTime DueDate { get; set; }
    // Đánh dấu đã thanh toán ngay khi tạo (nếu cần).
    public bool IsPaid { get; set; }
    // Các dòng chi tiết cần lưu.
    public List<InvoiceDetailCreateDto> Details { get; set; } = new();
}
