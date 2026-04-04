using ApartmentManagement.API.V1.Entities.Enums;

namespace ApartmentManagement.API.V1.DTOs.Apartments;

public class ApartmentCreateDto
{
    public string ApartmentNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal Area { get; set; }
    public ApartmentStatus Status { get; set; } = ApartmentStatus.Available;
    public string? Description { get; set; }
}
