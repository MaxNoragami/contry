using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Features.Auth.Handlers;

internal static class AuthCookieWriter
{
    public static void WriteSessionCookies(AuthCookieService authCookieService, HttpContext httpContext, AuthSessionResult session)
    {
        // The access cookie is kept as long as the refresh cookie so refresh and XSRF validation can still bind to the previous JWT identity.
        authCookieService.AppendAccessToken(httpContext.Response, session.AccessToken, session.RefreshTokenExpiresAtUtc);
        authCookieService.AppendRefreshToken(httpContext.Response, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
    }
}
