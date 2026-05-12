using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Features.Auth.Handlers;

public static class RegisterUserHandler
{
    public static async Task<IResult> HandleAsync(
        RegisterUserRequest request,
        RegisterUserCommandHandler registerUserCommandHandler,
        AuthCookieService authCookieService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (user, session) = await registerUserCommandHandler.HandleAsync(new RegisterUserCommand(request.Username, request.Email, request.Password), cancellationToken);
        AuthCookieWriter.WriteSessionCookies(authCookieService, httpContext, session);
        return Results.Created($"/users/{user.Id}", new AuthSessionResponse(UserResponse.FromUser(user), session.AccessTokenExpiresAtUtc, session.RefreshTokenExpiresAtUtc));
    }
}
