using Contry.Application.Auth;
using Contry.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Contry.Infrastructure.Authentication;

public sealed class AuthSessionOptions(IOptions<JwtOptions> options) : IAuthSessionOptions
{
    private readonly JwtOptions _options = options.Value;

    public int RefreshTokenLifetimeMinutes => _options.RefreshTokenLifetimeMinutes;
}
