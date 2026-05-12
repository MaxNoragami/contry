namespace Contry.Api.Features.Auth;

public sealed record AuthSessionResponse(UserResponse User, DateTimeOffset AccessTokenExpiresAtUtc, DateTimeOffset RefreshTokenExpiresAtUtc);
