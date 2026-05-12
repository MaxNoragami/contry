using Contry.Domain.Authentication;
using Contry.Domain.Users;

namespace Contry.Application.Auth;

public interface IAuthStore
{
    Task<(bool UsernameExists, bool EmailExists)> CheckRegistrationConflictsAsync(string normalizedUsername, string normalizedEmail, CancellationToken cancellationToken);

    Task AddUserAsync(User user, CancellationToken cancellationToken);

    Task<User?> FindUserByCredentialAsync(string normalizedCredential, CancellationToken cancellationToken);

    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<RefreshSession?> FindRefreshSessionByTokenHashAsync(string refreshTokenHash, bool includeUser, CancellationToken cancellationToken);

    Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken);

    Task RotateRefreshSessionAsync(RefreshSession currentSession, RefreshSession replacementSession, CancellationToken cancellationToken);

    Task RevokeSessionFamilyAsync(Guid sessionFamilyId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RevokeAllUserSessionsAsync(Guid userId, DateTimeOffset now, bool reuseDetected, CancellationToken cancellationToken);

    Task<int> DeleteExpiredRefreshSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
