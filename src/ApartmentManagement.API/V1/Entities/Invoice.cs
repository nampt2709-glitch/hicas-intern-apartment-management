namespace ApartmentManagement.API.V1.Entities;

// Hóa đơn theo căn hộ và kỳ cước; quan hệ tới chi tiết dòng và phản hồi tham chiếu.
public class Invoice : BaseEntity
{
    public Guid ApartmentId { get; set; }
    // Điều hướng: căn hộ bị tính phí (một căn — nhiều hóa đơn theo thời gian).
    public Apartment Apartment { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public DateOnly BillingMonth { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }

    // Điều hướng: các dòng chi tiết thành phần của hóa đơn.
    public ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
    // Điều hướng: phản hồi gắn tham chiếu tới hóa đơn này (một hóa đơn — nhiều Feedback có ReferenceInvoiceId).
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
