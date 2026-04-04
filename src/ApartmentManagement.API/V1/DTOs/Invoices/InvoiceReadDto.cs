namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceReadDto
{
    public Guid Id { get; set; }
    public Guid ApartmentId { get; set; }
    public string ApartmentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly BillingMonth { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<InvoiceDetailReadDto> Details { get; set; } = new();
}
