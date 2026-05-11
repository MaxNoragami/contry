namespace Contry.Application.Auth;

public interface IXsrfTokenService
{
    XsrfTokenResult CreateToken(AccessTokenIdentity identity);

    bool TryValidateToken(string token, AccessTokenIdentity identity, out DateTimeOffset expiresAtUtc);
}
