using Contry.Application.Errors;

namespace Contry.Application.Auth;

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
