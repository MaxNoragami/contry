using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Features.Auth.Handlers;

public static class GetXsrfTokenHandler
{
    public static async Task<IResult> HandleAsync(HttpContext httpContext, IXsrfTokenService xsrfTokenService, CurrentRefreshSessionService currentRefreshSessionService)
    {
        var session = await currentRefreshSessionService.GetSessionAsync(httpContext, allowRevoked: false, httpContext.RequestAborted);

        if (session is null)
        {
            throw new InvalidRefreshTokenException();
        }

        var xsrf = xsrfTokenService.CreateToken(new XsrfSessionBinding(session.UserId, session.SessionFamilyId, session.ExpiresAtUtc));
        return Results.Ok(new XsrfTokenResponse(xsrf.Token, xsrf.ExpiresAtUtc));
    }
}
