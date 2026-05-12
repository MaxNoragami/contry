using Contry.Domain.Authentication;
using Contry.Domain.Users;

namespace Contry.Application.Auth;

public sealed class RefreshSessionCommandHandler(
    IAuthStore authStore,
    IRefreshTokenService refreshTokenService,
    IAccessTokenService accessTokenService,
    IAuthSessionOptions authSessionOptions,
    TimeProvider timeProvider)
{
    private readonly IAuthStore _authStore = authStore;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly IAccessTokenService _accessTokenService = accessTokenService;
    private readonly IAuthSessionOptions _authSessionOptions = authSessionOptions;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<(User User, AuthSessionResult Session)> HandleAsync(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var refreshTokenHash = _refreshTokenService.HashToken(command.RefreshToken);
        var session = await _authStore.FindRefreshSessionByTokenHashAsync(refreshTokenHash, includeUser: true, cancellationToken);

        if (session is null || session.IsExpired(now))
        {
            throw new InvalidRefreshTokenException();
        }

        if (session.IsRevoked)
        {
            await _authStore.RevokeAllUserSessionsAsync(session.UserId, now, reuseDetected: true, cancellationToken);
            throw new RefreshTokenReuseDetectedException();
        }

        session.RevokedAtUtc = now;
        var replacementMaterial = _refreshTokenService.CreateToken();
        var replacementSession = new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = session.UserId,
            SessionFamilyId = session.SessionFamilyId,
            TokenHash = replacementMaterial.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_authSessionOptions.RefreshTokenLifetimeMinutes)
        };
        session.ReplacedBySessionId = replacementSession.Id;
        await _authStore.RotateRefreshSessionAsync(session, replacementSession, cancellationToken);

        var accessToken = _accessTokenService.CreateToken(session.UserId, session.User.Role.ToString().ToUpperInvariant(), out var accessTokenExpiresAtUtc);

        var issued = new AuthSessionResult(
            accessToken,
            accessTokenExpiresAtUtc,
            replacementMaterial.Token,
            replacementSession.ExpiresAtUtc);

        return (session.User, issued);
    }
}
