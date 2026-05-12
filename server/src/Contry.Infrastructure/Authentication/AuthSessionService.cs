using Contry.Application.Auth;
using Contry.Application.Errors;
using Contry.Domain.Authentication;
using Contry.Domain.Users;
using Contry.Infrastructure.Configuration;
using Contry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Contry.Infrastructure.Authentication;

public sealed class AuthSessionService(
    ContryDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokenService,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider,
    IOptions<JwtOptions> jwtOptions)
{
    private readonly ContryDbContext _dbContext = dbContext;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IAccessTokenService _accessTokenService = accessTokenService;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<(User User, AuthSessionResult Session)> RegisterUserAsync(string username, string email, string password, CancellationToken cancellationToken)
    {
        var normalizedUsername = Normalize(username);
        var normalizedEmail = Normalize(email);

        var conflicts = await _dbContext.Users
            .Where(user => user.NormalizedUsername == normalizedUsername || user.NormalizedEmail == normalizedEmail)
            .Select(user => new { user.NormalizedUsername, user.NormalizedEmail })
            .ToListAsync(cancellationToken);

        if (conflicts.Any(conflict => conflict.NormalizedUsername == normalizedUsername))
        {
            throw new AuthConflictException("username", "A user with that username already exists.");
        }

        if (conflicts.Any(conflict => conflict.NormalizedEmail == normalizedEmail))
        {
            throw new AuthConflictException("email", "A user with that email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username.Trim(),
            NormalizedUsername = normalizedUsername,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(password),
            Role = UserRole.User,
            CreatedAtUtc = _timeProvider.GetUtcNow()
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var session = await IssueSessionAsync(user, cancellationToken);
        return (user, session);
    }

    public async Task<(User User, AuthSessionResult Session)> CreateSessionAsync(string credential, string password, CancellationToken cancellationToken)
    {
        var normalizedCredential = Normalize(credential);
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(entity => entity.NormalizedEmail == normalizedCredential || entity.NormalizedUsername == normalizedCredential, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var session = await IssueSessionAsync(user, cancellationToken);
        return (user, session);
    }

    public async Task<(User User, AuthSessionResult Session)> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var refreshTokenHash = _refreshTokenService.HashToken(refreshToken);

        var session = await _dbContext.RefreshSessions
            .Include(entity => entity.User)
            .SingleOrDefaultAsync(entity => entity.TokenHash == refreshTokenHash, cancellationToken);

        if (session is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (session.IsExpired(now))
        {
            throw new InvalidRefreshTokenException();
        }

        if (session.IsRevoked)
        {
            await RevokeAllUserSessionsAsync(session.UserId, now, reuseDetected: true, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new RefreshTokenReuseDetectedException();
        }

        var replacementSession = CreateRefreshSession(session.UserId, session.SessionFamilyId, now, out var replacementToken);
        session.RevokedAtUtc = now;
        session.ReplacedBySessionId = replacementSession.Id;

        _dbContext.RefreshSessions.Add(replacementSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenService.CreateToken(session.UserId, session.User.Role.ToString().ToUpperInvariant(), out var accessTokenExpiresAtUtc);

        return (session.User, new AuthSessionResult(
            accessToken,
            accessTokenExpiresAtUtc,
            replacementToken,
            replacementSession.ExpiresAtUtc));
    }

    public async Task RevokeCurrentSessionAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var refreshTokenHash = _refreshTokenService.HashToken(refreshToken);
        var session = await _dbContext.RefreshSessions.SingleOrDefaultAsync(entity => entity.TokenHash == refreshTokenHash, cancellationToken);

        if (session is null || session.IsExpired(now) || session.IsRevoked)
        {
            return;
        }

        await RevokeSessionFamilyAsync(session.SessionFamilyId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredRefreshSessionsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var expiredSessions = await _dbContext.RefreshSessions
            .ToListAsync(cancellationToken);

        var sessionsToDelete = expiredSessions
            .Where(session => session.ExpiresAtUtc <= now)
            .ToList();

        _dbContext.RefreshSessions.RemoveRange(sessionsToDelete);

        if (sessionsToDelete.Count == 0)
        {
            return 0;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return sessionsToDelete.Count;
    }

    private async Task<AuthSessionResult> IssueSessionAsync(User user, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var refreshSession = CreateRefreshSession(user.Id, Guid.NewGuid(), now, out var refreshToken);
        _dbContext.RefreshSessions.Add(refreshSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenService.CreateToken(user.Id, user.Role.ToString().ToUpperInvariant(), out var accessTokenExpiresAtUtc);

        return new AuthSessionResult(accessToken, accessTokenExpiresAtUtc, refreshToken, refreshSession.ExpiresAtUtc);
    }

    private RefreshSession CreateRefreshSession(Guid userId, Guid sessionFamilyId, DateTimeOffset now, out string token)
    {
        var material = _refreshTokenService.CreateToken();
        token = material.Token;

        return new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionFamilyId = sessionFamilyId,
            TokenHash = material.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_jwtOptions.RefreshTokenLifetimeMinutes)
        };
    }

    private async Task RevokeSessionFamilyAsync(Guid sessionFamilyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.RefreshSessions
            .Where(session => session.SessionFamilyId == sessionFamilyId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions.Where(session => session.RevokedAtUtc == null && session.ExpiresAtUtc > now))
        {
            session.RevokedAtUtc = now;
        }
    }

    private async Task RevokeAllUserSessionsAsync(Guid userId, DateTimeOffset now, bool reuseDetected, CancellationToken cancellationToken)
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
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

public sealed class AuthConflictException(string field, string message) : ConflictException(
    "/problems/auth/conflict",
    "Authentication conflict.",
    message)
{
    public string Field { get; } = field;

    public override IDictionary<string, object?> GetExtensions()
        => new Dictionary<string, object?>
        {
            ["field"] = Field
        };
}

public sealed class InvalidCredentialsException() : UnauthorizedException(
    "/problems/auth/invalid-credentials",
    "Invalid credentials.",
    "The provided username/email or password is incorrect.");

public sealed class InvalidRefreshTokenException() : UnauthorizedException(
    "/problems/auth/invalid-refresh-token",
    "Invalid refresh token.",
    "The refresh token is missing, expired, revoked, or otherwise invalid.");

public sealed class RefreshTokenReuseDetectedException() : UnauthorizedException(
    "/problems/auth/refresh-token-reuse",
    "Refresh token reuse detected.",
    "The refresh token was already used or revoked, and all user sessions have been invalidated as a security precaution.");
