namespace Contry.Infrastructure.Configuration;

public sealed class AuthCleanupOptions
{
    public const string SectionName = "AuthCleanup";

    public int IntervalMinutes { get; set; } = 5;
}
