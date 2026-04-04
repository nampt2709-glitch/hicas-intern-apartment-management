using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Entities;

public class Resident : BaseEntity
{
    public Guid ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public Guid? UserId { get; set; }
    public ApplicationUser? Account { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsOwner { get; set; }
    public DateTime MoveInDate { get; set; } = DateTime.UtcNow;
    public DateTime? MoveOutDate { get; set; }
}
