using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Contry.Application.Auth;
using Contry.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Contry.Infrastructure.Authentication;

public sealed class JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public string CreateToken(Guid userId, string role, out DateTimeOffset expiresAtUtc)
    {
        if (!IsAllowedRole(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "JWT role must be USER or ADMIN.");
        }

        var now = _timeProvider.GetUtcNow();
        expiresAtUtc = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var jwtId = Guid.NewGuid().ToString("N");

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, jwtId)
            ]),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAtUtc.UtcDateTime,
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            SigningCredentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        var token = _tokenHandler.CreateToken(descriptor);
        return _tokenHandler.WriteToken(token);
    }

    public bool TryReadIdentity(string token, bool validateLifetime, out AccessTokenIdentity? identity)
    {
        identity = null;

        try
        {
            var principal = _tokenHandler.ValidateToken(token, CreateValidationParameters(validateLifetime), out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt)
            {
                return false;
            }

            var sub = FirstNonEmpty(jwt.Subject, principal.FindFirstValue(JwtRegisteredClaimNames.Sub), principal.FindFirstValue(ClaimTypes.NameIdentifier));
            var role = FirstNonEmpty(principal.FindFirstValue(ClaimTypes.Role), principal.FindFirstValue("role"), jwt.Claims.FirstOrDefault(claim => claim.Type is ClaimTypes.Role or "role")?.Value);
            var jti = FirstNonEmpty(jwt.Id, principal.FindFirstValue(JwtRegisteredClaimNames.Jti));

            if (!Guid.TryParse(sub, out var userId) || string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(jti))
            {
                return false;
            }

            if (!IsAllowedRole(role))
            {
                return false;
            }

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(jwt.Payload.Expiration ?? 0L);
            identity = new AccessTokenIdentity(userId, role, jti, expiresAt);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public TokenValidationParameters CreateValidationParameters(bool validateLifetime)
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSigningKey(),
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };

    private SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(_options.Secret));

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsAllowedRole(string role) => role is "USER" or "ADMIN";
}
