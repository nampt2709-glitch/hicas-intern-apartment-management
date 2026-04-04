namespace ApartmentManagement.API.V1.Entities;

public class Invoice : BaseEntity
{
    public Guid ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public DateOnly BillingMonth { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }

    public ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
