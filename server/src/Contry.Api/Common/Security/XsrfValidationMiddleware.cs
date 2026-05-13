using Contry.Api.Common.Errors;
using Contry.Api.Common.OpenApi;
using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;

namespace Contry.Api.Common.Security;

public sealed class XsrfValidationMiddleware(
    RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(
        HttpContext httpContext,
        IXsrfTokenService xsrfTokenService,
        CurrentRefreshSessionService currentRefreshSessionService)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<RequireXsrfMetadata>() is null)
        {
            await _next(httpContext);
            return;
        }

        if (!httpContext.Request.Headers.TryGetValue("X-XSRF-TOKEN", out var xsrfHeader) || string.IsNullOrWhiteSpace(xsrfHeader))
        {
            throw new MissingXsrfTokenException();
        }

        var session = await currentRefreshSessionService.GetSessionAsync(httpContext, allowRevoked: true, httpContext.RequestAborted);
        if (session is null)
        {
            throw new InvalidRefreshTokenException();
        }

        var binding = new XsrfSessionBinding(session.UserId, session.SessionFamilyId, session.ExpiresAtUtc);
        if (!xsrfTokenService.TryValidateToken(xsrfHeader.ToString(), binding, out _))
        {
            throw new InvalidXsrfTokenException();
        }

        await _next(httpContext);
    }
}
