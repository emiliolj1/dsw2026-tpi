namespace Dsw2026Tpi.Application.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicyOptions AdminLogin { get; init; } = new();
    public RateLimitPolicyOptions PatientLogin { get; init; } = new();
    public RateLimitPolicyOptions AppointmentBooking { get; init; } = new();
    public RateLimitPolicyOptions General { get; init; } = new();
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; init; }
    public int WindowSeconds { get; init; }
}
