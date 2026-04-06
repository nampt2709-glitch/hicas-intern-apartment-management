// DTO cập nhật hóa đơn và các dòng chi tiết kèm theo.
namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceUpdateDto
{
    // Căn hộ bị tính phí.
    public Guid ApartmentId { get; set; }
    // Tiêu đề / mô tả ngắn hóa đơn.
    public string Title { get; set; } = string.Empty;
    // Tháng kỳ cước (chỉ ngày-tháng-năm theo ngày đầu tháng).
    public DateOnly BillingMonth { get; set; }
    // Ngày phát hành hóa đơn.
    public DateTime IssueDate { get; set; }
    // Hạn thanh toán.
    public DateTime DueDate { get; set; }
    // Đã thanh toán hay chưa.
    public bool IsPaid { get; set; }
    // Danh sách dòng chi tiết (dịch vụ, số lượng, đơn giá).
    public List<InvoiceDetailCreateDto> Details { get; set; } = new();
}
