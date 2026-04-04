namespace ApartmentManagement.API.V1.Entities;

public class InvoiceDetail : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public Guid UtilityServiceId { get; set; }
    public UtilityService UtilityService { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public string? Note { get; set; }
}
