namespace Contry.Infrastructure.Configuration;

public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    public string AccessCookieName { get; set; } = "contry_access";

    public string RefreshCookieName { get; set; } = "contry_refresh";

    public string? Domain { get; set; }

    public string Path { get; set; } = "/";

    public string SameSite { get; set; } = "Lax";

    public string SecurePolicy { get; set; } = "SameAsRequest";
}
