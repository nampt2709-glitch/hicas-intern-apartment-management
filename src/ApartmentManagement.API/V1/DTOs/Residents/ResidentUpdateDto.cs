namespace ApartmentManagement.API.V1.DTOs.Residents;

public class ResidentUpdateDto
{
    public Guid ApartmentId { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsOwner { get; set; }
    public DateTime MoveInDate { get; set; } = DateTime.UtcNow;
    public DateTime? MoveOutDate { get; set; }
}
