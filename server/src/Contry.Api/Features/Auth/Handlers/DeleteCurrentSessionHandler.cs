using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Features.Auth.Handlers;

public static class DeleteCurrentSessionHandler
{
    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        RevokeCurrentSessionCommandHandler revokeCurrentSessionCommandHandler,
        AuthCookieService authCookieService,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(authCookieService.RefreshCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            authCookieService.ClearAuthCookies(httpContext.Response);
            return Results.NoContent();
        }

        await revokeCurrentSessionCommandHandler.HandleAsync(new RevokeCurrentSessionCommand(refreshToken), cancellationToken);
        authCookieService.ClearAuthCookies(httpContext.Response);
        return Results.NoContent();
    }
}
