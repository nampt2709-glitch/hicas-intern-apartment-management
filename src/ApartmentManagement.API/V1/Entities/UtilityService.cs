namespace ApartmentManagement.API.V1.Entities;

public class UtilityService : BaseEntity
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
