namespace ApartmentManagement.Settings;

// Cấu hình giới hạn cửa sổ cố định: theo user đã đăng nhập (JWT sub) hoặc theo IP khi ẩn danh.
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Số lần gọi API tối đa mỗi user mỗi cửa sổ (mobile/web/polling).
    public int ApiPermitsPerMinutePerUser { get; set; } = 800;

    // Số lần gọi API tối đa mỗi IP ẩn danh mỗi cửa sổ (Swagger, probe...).
    public int ApiPermitsPerMinutePerIpAnonymous { get; set; } = 120;

    // Số lần đăng nhập/làm mới token tối đa mỗi IP mỗi cửa sổ (chống dò mật khẩu).
    public int AuthPermitsPerMinutePerIp { get; set; } = 25;

    public int WindowMinutes { get; set; } = 1;
}
