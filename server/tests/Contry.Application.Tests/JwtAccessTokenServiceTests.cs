using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Contry.Infrastructure.Authentication;
using Contry.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Contry.Application.Tests;

public sealed class JwtAccessTokenServiceTests
{
    [Fact]
    public void CreateToken_WritesExpectedClaims_AndCanReadIdentity()
    {
        var now = new DateTimeOffset(2026, 5, 11, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var options = Options.Create(new JwtOptions
        {
            Issuer = "contry-api",
            Audience = "contry-client",
            Secret = "super-secret-key-for-tests-only-1234567890",
            AccessTokenLifetimeMinutes = 1,
            RefreshTokenLifetimeMinutes = 5
        });

        var service = new JwtAccessTokenService(options, timeProvider);
        var userId = Guid.NewGuid();

        var token = service.CreateToken(userId, "USER", out var expiresAtUtc);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Id));
        Assert.Contains(jwt.Claims, claim => claim.Type is ClaimTypes.Role or "role" && claim.Value == "USER");
        Assert.Equal(now.AddMinutes(1), expiresAtUtc);

        var couldRead = service.TryReadIdentity(token, validateLifetime: false, out var identity);

        Assert.True(couldRead);
        Assert.NotNull(identity);
        Assert.Equal(userId, identity!.UserId);
        Assert.Equal("USER", identity.Role);
        Assert.Equal(expiresAtUtc, identity.ExpiresAtUtc);
    }

    [Fact]
    public void CreateToken_RejectsUnexpectedRole()
    {
        var service = new JwtAccessTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "contry-api",
                Audience = "contry-client",
                Secret = "super-secret-key-for-tests-only-1234567890",
                AccessTokenLifetimeMinutes = 1,
                RefreshTokenLifetimeMinutes = 5
            }),
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CreateToken(Guid.NewGuid(), "HACKER", out _));
    }
}
