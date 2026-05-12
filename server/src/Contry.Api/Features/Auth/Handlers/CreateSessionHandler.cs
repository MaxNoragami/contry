using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Features.Auth.Handlers;

public static class CreateSessionHandler
{
    public static async Task<IResult> HandleAsync(
        CreateSessionRequest request,
        CreateSessionCommandHandler createSessionCommandHandler,
        AuthCookieService authCookieService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (user, session) = await createSessionCommandHandler.HandleAsync(new CreateSessionCommand(request.Credential, request.Password), cancellationToken);
        AuthCookieWriter.WriteSessionCookies(authCookieService, httpContext, session);
        return Results.Ok(new AuthSessionResponse(UserResponse.FromUser(user), session.AccessTokenExpiresAtUtc, session.RefreshTokenExpiresAtUtc));
    }
}
