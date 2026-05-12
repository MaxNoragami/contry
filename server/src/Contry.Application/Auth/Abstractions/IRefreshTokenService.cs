namespace Contry.Application.Auth;

public interface IRefreshTokenService
{
    RefreshTokenMaterial CreateToken();

    string HashToken(string token);
}
