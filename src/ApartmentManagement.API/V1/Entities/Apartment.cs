using ApartmentManagement.API.V1.Entities.Enums;

namespace ApartmentManagement.API.V1.Entities;

// Căn hộ trong tòa nhà: trạng thái vận hành và các tập con ảnh, cư dân, hóa đơn, phản hồi.
public class Apartment : BaseEntity
{
    public string ApartmentNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal Area { get; set; }
    // Trạng thái căn — xem enum ApartmentStatus (Available/Occupied/Maintenance).
    public ApartmentStatus Status { get; set; } = ApartmentStatus.Available;
    public string? Description { get; set; }
    public string? CoverImagePath { get; set; }

    // Điều hướng: album ảnh của căn (một căn — nhiều ApartmentImage).
    public ICollection<ApartmentImage> Images { get; set; } = new List<ApartmentImage>();
    // Điều hướng: cư dân gắn với căn (một căn — nhiều Resident).
    public ICollection<Resident> Residents { get; set; } = new List<Resident>();
    // Điều hướng: các hóa đơn phát sinh cho căn (một căn — nhiều Invoice).
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    // Điều hướng: phản hồi tham chiếu trực tiếp tới căn (một căn — nhiều Feedback có ReferenceApartmentId).
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
