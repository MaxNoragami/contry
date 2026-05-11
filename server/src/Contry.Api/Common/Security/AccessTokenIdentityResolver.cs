using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Contry.Application.Auth;

namespace Contry.Api.Common.Security;

public static class AccessTokenIdentityResolver
{
    public static bool TryResolve(ClaimsPrincipal principal, out AccessTokenIdentity? identity)
    {
        identity = null;

        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = principal.FindFirstValue(ClaimTypes.Role) ?? principal.FindFirstValue("role");
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var exp = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);

        if (!Guid.TryParse(sub, out var userId) || string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(jti) || !long.TryParse(exp, out var expUnix))
        {
            return false;
        }

        if (role is not "USER" and not "ADMIN")
        {
            return false;
        }

        identity = new AccessTokenIdentity(userId, role, jti, DateTimeOffset.FromUnixTimeSeconds(expUnix));
        return true;
    }
}
