using Contry.Api.Common.Security;
using Contry.Application.Auth;
using Contry.Application.Errors;

namespace Contry.Api.Features.TestRecords.Handlers;

internal static class TestRecordIdentityResolver
{
    public static AccessTokenIdentity RequireIdentity(HttpContext httpContext)
    {
        if (!AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity) || identity is null)
        {
            throw new InvalidAccessTokenException();
        }

        return identity;
    }
}
