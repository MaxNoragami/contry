using Contry.Api.Common.Security;
using Contry.Api.Common.Errors;
using Contry.Application.Auth;
using Contry.Application.Errors;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Common.EndpointFilters;

public sealed class XsrfEndpointFilter(
    IXsrfTokenService xsrfTokenService,
    IAccessTokenService accessTokenService,
    AuthCookieService cookieService) : IEndpointFilter
{
    private readonly IXsrfTokenService _xsrfTokenService = xsrfTokenService;
    private readonly IAccessTokenService _accessTokenService = accessTokenService;
    private readonly AuthCookieService _cookieService = cookieService;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue("X-XSRF-TOKEN", out var xsrfHeader) || string.IsNullOrWhiteSpace(xsrfHeader))
        {
            throw new MissingXsrfTokenException();
        }

        if (!TryResolveIdentity(httpContext, out var identity) || identity is null)
        {
            throw new InvalidAccessTokenException();
        }

        if (!_xsrfTokenService.TryValidateToken(xsrfHeader.ToString(), identity, out _))
        {
            throw new InvalidXsrfTokenException();
        }

        return await next(context);
    }

    private bool TryResolveIdentity(HttpContext httpContext, out AccessTokenIdentity? identity)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true && AccessTokenIdentityResolver.TryResolve(httpContext.User, out identity))
        {
            return true;
        }

        identity = null;

        if (!httpContext.Request.Cookies.TryGetValue(_cookieService.AccessCookieName, out var accessToken) || string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        return _accessTokenService.TryReadIdentity(accessToken, validateLifetime: false, out identity);
    }
}
