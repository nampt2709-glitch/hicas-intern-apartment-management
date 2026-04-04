namespace ApartmentManagement.API.V1.DTOs.Auth;

public class CurrentUserDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarPath { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}
