using Contry.Domain.Authentication;
using Contry.Domain.Users;

namespace Contry.Application.Auth;

public sealed class AuthSessionIssuer(
    IAuthStore authStore,
    IAccessTokenService accessTokenService,
    IRefreshTokenService refreshTokenService,
    IAuthSessionOptions authSessionOptions,
    TimeProvider timeProvider)
{
    private readonly IAuthStore _authStore = authStore;
    private readonly IAccessTokenService _accessTokenService = accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly IAuthSessionOptions _authSessionOptions = authSessionOptions;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<AuthSessionResult> IssueSessionAsync(User user, Guid sessionFamilyId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var material = _refreshTokenService.CreateToken();
        var refreshSession = new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SessionFamilyId = sessionFamilyId,
            TokenHash = material.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_authSessionOptions.RefreshTokenLifetimeMinutes)
        };

        await _authStore.AddRefreshSessionAsync(refreshSession, cancellationToken);
        var accessToken = _accessTokenService.CreateToken(user.Id, user.Role.ToString().ToUpperInvariant(), out var accessTokenExpiresAtUtc);
        return new AuthSessionResult(accessToken, accessTokenExpiresAtUtc, material.Token, refreshSession.ExpiresAtUtc);
    }
}
