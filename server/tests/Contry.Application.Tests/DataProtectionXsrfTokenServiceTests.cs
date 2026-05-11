using Contry.Application.Auth;
using Contry.Infrastructure.Xsrf;
using Microsoft.AspNetCore.DataProtection;

namespace Contry.Application.Tests;

public sealed class DataProtectionXsrfTokenServiceTests
{
    [Fact]
    public void CreateToken_ValidatesOnlyForMatchingIdentity()
    {
        var provider = DataProtectionProvider.Create("Contry.Xsrf.Tests");
        var service = new DataProtectionXsrfTokenService(provider);
        var identity = new AccessTokenIdentity(Guid.NewGuid(), "USER", "jwt-1", DateTimeOffset.UtcNow.AddMinutes(1));

        var token = service.CreateToken(identity);

        var valid = service.TryValidateToken(token.Token, identity, out var expiresAtUtc);
        var invalid = service.TryValidateToken(token.Token, identity with { JwtId = "jwt-2" }, out _);

        Assert.True(valid);
        Assert.Equal(identity.ExpiresAtUtc, expiresAtUtc);
        Assert.False(invalid);
    }
}
