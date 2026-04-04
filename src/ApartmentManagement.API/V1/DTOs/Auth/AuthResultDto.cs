namespace ApartmentManagement.API.V1.DTOs.Auth;

public class AuthResultDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public TokenPairDto Tokens { get; set; } = new();
}
