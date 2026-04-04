namespace ApartmentManagement.API.V1.DTOs.Services;

public class UtilityServiceReadDto
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
