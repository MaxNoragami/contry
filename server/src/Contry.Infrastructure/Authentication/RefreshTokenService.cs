using System.Security.Cryptography;
using System.Text;
using Contry.Application.Auth;

namespace Contry.Infrastructure.Authentication;

public sealed class RefreshTokenService : IRefreshTokenService
{
    public RefreshTokenMaterial CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new RefreshTokenMaterial(token, HashToken(token));
    }

    public string HashToken(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }
}
