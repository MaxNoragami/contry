using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Features.Auth.Handlers;

public static class RefreshSessionHandler
{
    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        RefreshSessionCommandHandler refreshSessionCommandHandler,
        AuthCookieService authCookieService,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(authCookieService.RefreshCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            authCookieService.ClearAuthCookies(httpContext.Response);
            throw new InvalidRefreshTokenException();
        }

        try
        {
            var (user, session) = await refreshSessionCommandHandler.HandleAsync(new RefreshSessionCommand(refreshToken), cancellationToken);
            AuthCookieWriter.WriteSessionCookies(authCookieService, httpContext, session);
            return Results.Ok(new AuthSessionResponse(UserResponse.FromUser(user), session.AccessTokenExpiresAtUtc, session.RefreshTokenExpiresAtUtc));
        }
        catch
        {
            authCookieService.ClearAuthCookies(httpContext.Response);
            throw;
        }
    }
}
