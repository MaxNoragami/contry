namespace Contry.Application.Auth;

public sealed record AccessTokenIdentity(Guid UserId, string Role, string JwtId, DateTimeOffset ExpiresAtUtc);
