using Contry.Api.Common.Security;
using Contry.Api.Common.Errors;
using Contry.Application.Auth;
using Contry.Application.Errors;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Common.EndpointFilters;

public sealed class XsrfEndpointFilter(
    IXsrfTokenService xsrfTokenService,
    CurrentRefreshSessionService currentRefreshSessionService) : IEndpointFilter
{
    private readonly IXsrfTokenService _xsrfTokenService = xsrfTokenService;
    private readonly CurrentRefreshSessionService _currentRefreshSessionService = currentRefreshSessionService;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue("X-XSRF-TOKEN", out var xsrfHeader) || string.IsNullOrWhiteSpace(xsrfHeader))
        {
            throw new MissingXsrfTokenException();
        }

        var session = await _currentRefreshSessionService.GetSessionAsync(httpContext, allowRevoked: true, httpContext.RequestAborted);

        if (session is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            if (!AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity) || identity is null || identity.UserId != session.UserId)
            {
                throw new InvalidAccessTokenException();
            }
        }

        var binding = new XsrfSessionBinding(session.UserId, session.SessionFamilyId, session.ExpiresAtUtc);

        if (!_xsrfTokenService.TryValidateToken(xsrfHeader.ToString(), binding, out _))
        {
            throw new InvalidXsrfTokenException();
        }

        return await next(context);
    }
}
