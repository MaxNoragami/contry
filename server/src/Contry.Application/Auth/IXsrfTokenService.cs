namespace Contry.Application.Auth;

public interface IXsrfTokenService
{
    XsrfTokenResult CreateToken(XsrfSessionBinding binding);

    bool TryValidateToken(string token, XsrfSessionBinding binding, out DateTimeOffset expiresAtUtc);
}
