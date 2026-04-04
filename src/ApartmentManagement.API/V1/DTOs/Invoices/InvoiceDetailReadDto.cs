namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceDetailReadDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid UtilityServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public string? Note { get; set; }
}
