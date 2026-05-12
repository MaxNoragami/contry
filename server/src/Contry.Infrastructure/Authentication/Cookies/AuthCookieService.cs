using Contry.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Contry.Infrastructure.Authentication;

public sealed class AuthCookieService(IOptions<AuthCookieOptions> options)
{
    private readonly AuthCookieOptions _options = options.Value;

    public void AppendAccessToken(HttpResponse response, string token, DateTimeOffset expiresAtUtc)
    {
        response.Cookies.Append(_options.AccessCookieName, token, CreateCookieOptions(expiresAtUtc));
    }

    public void AppendRefreshToken(HttpResponse response, string token, DateTimeOffset expiresAtUtc)
    {
        response.Cookies.Append(_options.RefreshCookieName, token, CreateCookieOptions(expiresAtUtc));
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Delete(_options.AccessCookieName, CreateCookieOptions(DateTimeOffset.UnixEpoch));
        response.Cookies.Delete(_options.RefreshCookieName, CreateCookieOptions(DateTimeOffset.UnixEpoch));
    }

    public string AccessCookieName => _options.AccessCookieName;

    public string RefreshCookieName => _options.RefreshCookieName;

    private CookieOptions CreateCookieOptions(DateTimeOffset expiresAtUtc)
        => new()
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = ParseSecurePolicy() != CookieSecurePolicy.None,
            SameSite = ParseSameSiteMode(),
            Path = _options.Path,
            Domain = string.IsNullOrWhiteSpace(_options.Domain) ? null : _options.Domain,
            Expires = expiresAtUtc.UtcDateTime
        };

    private SameSiteMode ParseSameSiteMode()
        => Enum.TryParse<SameSiteMode>(_options.SameSite, true, out var mode) ? mode : SameSiteMode.Lax;

    private CookieSecurePolicy ParseSecurePolicy()
        => Enum.TryParse<CookieSecurePolicy>(_options.SecurePolicy, true, out var policy)
            ? policy
            : CookieSecurePolicy.SameAsRequest;
}
