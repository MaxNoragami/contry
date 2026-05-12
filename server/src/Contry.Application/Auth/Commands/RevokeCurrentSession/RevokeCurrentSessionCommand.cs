namespace Contry.Application.Auth;

public sealed record RevokeCurrentSessionCommand(string RefreshToken);
