namespace ApartmentManagement.Settings;

/// <summary>Fixed-window limits. API is partitioned per authenticated user (JWT sub) or per IP when anonymous.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Authenticated API calls per user per window (mobile + web + polling).</summary>
    public int ApiPermitsPerMinutePerUser { get; set; } = 800;

    /// <summary>Unauthenticated traffic per client IP per window (rare for this API; Swagger/probes).</summary>
    public int ApiPermitsPerMinutePerIpAnonymous { get; set; } = 120;

    /// <summary>Login/refresh attempts per IP per window (credential-stuffing protection).</summary>
    public int AuthPermitsPerMinutePerIp { get; set; } = 25;

    public int WindowMinutes { get; set; } = 1;
}
