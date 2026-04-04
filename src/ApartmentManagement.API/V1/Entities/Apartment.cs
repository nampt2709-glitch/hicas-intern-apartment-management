using ApartmentManagement.API.V1.Entities.Enums;

namespace ApartmentManagement.API.V1.Entities;

public class Apartment : BaseEntity
{
    public string ApartmentNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal Area { get; set; }
    public ApartmentStatus Status { get; set; } = ApartmentStatus.Available;
    public string? Description { get; set; }
    public string? CoverImagePath { get; set; }

    public ICollection<ApartmentImage> Images { get; set; } = new List<ApartmentImage>();
    public ICollection<Resident> Residents { get; set; } = new List<Resident>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
