namespace TaskManager.Api.Configuration;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    public const string PolicyName = "auth-credentials";

    // Credential submissions (login + register) allowed per client IP per window.
    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    // Sliding-window granularity; 6 segments of a 60 s window move every 10 s.
    public int SegmentsPerWindow { get; set; } = 6;
}
