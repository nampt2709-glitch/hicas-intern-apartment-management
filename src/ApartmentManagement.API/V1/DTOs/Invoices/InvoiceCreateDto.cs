namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceCreateDto
{
    public Guid ApartmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly BillingMonth { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
    public List<InvoiceDetailCreateDto> Details { get; set; } = new();
}
