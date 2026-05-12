using Contry.Application.Auth;
using Contry.Domain.Authentication;
using Contry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Contry.Infrastructure.Authentication;

public sealed class CurrentRefreshSessionService(
    ContryDbContext dbContext,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider,
    AuthCookieService authCookieService)
{
    private readonly ContryDbContext _dbContext = dbContext;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly AuthCookieService _authCookieService = authCookieService;

    public async Task<RefreshSession?> GetSessionAsync(HttpContext httpContext, bool allowRevoked, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(_authCookieService.RefreshCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var refreshTokenHash = _refreshTokenService.HashToken(refreshToken);
        var session = await _dbContext.RefreshSessions
            .SingleOrDefaultAsync(entity => entity.TokenHash == refreshTokenHash, cancellationToken);

        if (session is null || session.IsExpired(_timeProvider.GetUtcNow()))
        {
            return null;
        }

        if (!allowRevoked && session.IsRevoked)
        {
            return null;
        }

        return session;
    }
}
