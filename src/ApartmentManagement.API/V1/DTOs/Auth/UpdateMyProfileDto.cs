namespace ApartmentManagement.API.V1.DTOs.Auth;

/// <summary>Self-service profile update (no role, email, or admin-only fields).</summary>
public sealed class UpdateMyProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}
