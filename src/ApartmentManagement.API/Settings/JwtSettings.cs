namespace ApartmentManagement.Settings;

// Bind từ cấu hình "Jwt": khóa ký, issuer/audience, thời hạn access và refresh.
public sealed class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
