using Contry.Api.Common.Security;
using Contry.Application.Auth;
using Contry.Application.Errors;

namespace Contry.Api.Features.Auth.Handlers;

public static class GetCurrentUserHandler
{
    public static async Task<IResult> HandleAsync(HttpContext httpContext, GetCurrentUserQueryHandler getCurrentUserQueryHandler, CancellationToken cancellationToken)
    {
        if (!AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity) || identity is null)
        {
            throw new InvalidAccessTokenException();
        }

        var user = await getCurrentUserQueryHandler.HandleAsync(new GetCurrentUserQuery(identity.UserId), cancellationToken);

        return Results.Ok(UserResponse.FromUser(user));
    }
}
