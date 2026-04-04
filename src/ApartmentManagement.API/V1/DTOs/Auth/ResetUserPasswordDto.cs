namespace ApartmentManagement.API.V1.DTOs.Auth;

public sealed class ResetUserPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}

