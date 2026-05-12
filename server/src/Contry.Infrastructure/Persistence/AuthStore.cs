using Contry.Application.Auth;
using Contry.Domain.Authentication;
using Contry.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Contry.Infrastructure.Persistence;

public sealed class AuthStore(ContryDbContext dbContext) : IAuthStore
{
    private readonly ContryDbContext _dbContext = dbContext;

    public async Task<(bool UsernameExists, bool EmailExists)> CheckRegistrationConflictsAsync(string normalizedUsername, string normalizedEmail, CancellationToken cancellationToken)
    {
        var conflicts = await _dbContext.Users
            .Where(user => user.NormalizedUsername == normalizedUsername || user.NormalizedEmail == normalizedEmail)
            .Select(user => new { user.NormalizedUsername, user.NormalizedEmail })
            .ToListAsync(cancellationToken);

        return (
            conflicts.Any(conflict => conflict.NormalizedUsername == normalizedUsername),
            conflicts.Any(conflict => conflict.NormalizedEmail == normalizedEmail));
    }

    public async Task AddUserAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<User?> FindUserByCredentialAsync(string normalizedCredential, CancellationToken cancellationToken)
        => _dbContext.Users
            .SingleOrDefaultAsync(entity => entity.NormalizedEmail == normalizedCredential || entity.NormalizedUsername == normalizedCredential, cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.Users.SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

    public Task<RefreshSession?> FindRefreshSessionByTokenHashAsync(string refreshTokenHash, bool includeUser, CancellationToken cancellationToken)
    {
        var query = _dbContext.RefreshSessions.AsQueryable();

        if (includeUser)
        {
            query = query.Include(entity => entity.User);
        }

        return query.SingleOrDefaultAsync(entity => entity.TokenHash == refreshTokenHash, cancellationToken);
    }

    public async Task AddRefreshSessionAsync(RefreshSession session, CancellationToken cancellationToken)
    {
        _dbContext.RefreshSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RotateRefreshSessionAsync(RefreshSession currentSession, RefreshSession replacementSession, CancellationToken cancellationToken)
    {
        _dbContext.RefreshSessions.Update(currentSession);
        _dbContext.RefreshSessions.Add(replacementSession);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeSessionFamilyAsync(Guid sessionFamilyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.RefreshSessions
            .Where(session => session.SessionFamilyId == sessionFamilyId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions.Where(session => session.RevokedAtUtc == null && session.ExpiresAtUtc > now))
        {
            session.RevokedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllUserSessionsAsync(Guid userId, DateTimeOffset now, bool reuseDetected, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.RefreshSessions
            .Where(session => session.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions.Where(session => session.RevokedAtUtc == null && session.ExpiresAtUtc > now))
        {
            session.RevokedAtUtc = now;

            if (reuseDetected)
            {
                session.ReuseDetectedAtUtc = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredRefreshSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expiredSessions = await _dbContext.RefreshSessions.ToListAsync(cancellationToken);
        var sessionsToDelete = expiredSessions.Where(session => session.ExpiresAtUtc <= now).ToList();

        _dbContext.RefreshSessions.RemoveRange(sessionsToDelete);

        if (sessionsToDelete.Count == 0)
        {
            return 0;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return sessionsToDelete.Count;
    }
}
