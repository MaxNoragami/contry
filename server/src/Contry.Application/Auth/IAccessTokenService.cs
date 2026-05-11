namespace Contry.Application.Auth;

public interface IAccessTokenService
{
    string CreateToken(Guid userId, string role, out DateTimeOffset expiresAtUtc);

    bool TryReadIdentity(string token, bool validateLifetime, out AccessTokenIdentity? identity);
}
