namespace ApartmentManagement.API.V1.Entities;

// Một dòng chi tiết trên hóa đơn: liên kết hóa đơn và dịch vụ tiện ích, lưu số lượng và thành tiền.
public class InvoiceDetail : BaseEntity
{
    public Guid InvoiceId { get; set; }
    // Điều hướng: hóa đơn chứa dòng này (một hóa đơn — nhiều chi tiết).
    public Invoice Invoice { get; set; } = null!;

    public Guid UtilityServiceId { get; set; }
    // Điều hướng: dịch vụ được tính tiền (nhiều dòng chi tiết có thể cùng một dịch vụ trên các hóa đơn khác nhau).
    public UtilityService UtilityService { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public string? Note { get; set; }
}
