// DTO đọc hóa đơn kèm tóm tắt căn hộ và chi tiết các dòng.
namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceReadDto
{
    // Khóa chính hóa đơn.
    public Guid Id { get; set; }
    // Mã căn hộ.
    public Guid ApartmentId { get; set; }
    // Số hoặc mã căn hộ (hiển thị).
    public string ApartmentNumber { get; set; } = string.Empty;
    // Tiêu đề hóa đơn.
    public string Title { get; set; } = string.Empty;
    // Kỳ thanh toán (tháng).
    public DateOnly BillingMonth { get; set; }
    // Ngày lập.
    public DateTime IssueDate { get; set; }
    // Hạn thanh toán.
    public DateTime DueDate { get; set; }
    // Tổng tiền (đã tính từ chi tiết).
    public decimal TotalAmount { get; set; }
    // Trạng thái đã trả.
    public bool IsPaid { get; set; }
    // Thời điểm ghi nhận thanh toán (nếu đã trả).
    public DateTime? PaidAt { get; set; }
    // Các dòng chi tiết hóa đơn.
    public List<InvoiceDetailReadDto> Details { get; set; } = new();
}
