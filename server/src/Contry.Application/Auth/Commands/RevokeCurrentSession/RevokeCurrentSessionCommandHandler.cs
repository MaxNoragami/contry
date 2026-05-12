namespace Contry.Application.Auth;

public sealed class RevokeCurrentSessionCommandHandler(
    IAuthStore authStore,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider)
{
    private readonly IAuthStore _authStore = authStore;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task HandleAsync(RevokeCurrentSessionCommand command, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var refreshTokenHash = _refreshTokenService.HashToken(command.RefreshToken);
        var session = await _authStore.FindRefreshSessionByTokenHashAsync(refreshTokenHash, includeUser: false, cancellationToken);

        if (session is null || session.IsExpired(now) || session.IsRevoked)
        {
            return;
        }

        await _authStore.RevokeSessionFamilyAsync(session.SessionFamilyId, now, cancellationToken);
    }
}
