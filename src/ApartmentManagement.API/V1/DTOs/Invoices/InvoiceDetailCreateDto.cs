namespace ApartmentManagement.API.V1.DTOs.Invoices;

public class InvoiceDetailCreateDto
{
    public Guid UtilityServiceId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
}
