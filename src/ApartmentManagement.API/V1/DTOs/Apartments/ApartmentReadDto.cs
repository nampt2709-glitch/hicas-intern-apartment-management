using ApartmentManagement.API.V1.Entities.Enums;

namespace ApartmentManagement.API.V1.DTOs.Apartments;

public class ApartmentReadDto
{
    public Guid Id { get; set; }
    public string ApartmentNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal Area { get; set; }
    public ApartmentStatus Status { get; set; }
    public string? Description { get; set; }
    public string? CoverImagePath { get; set; }
    public int ResidentCount { get; set; }
    public int InvoiceCount { get; set; }
}
