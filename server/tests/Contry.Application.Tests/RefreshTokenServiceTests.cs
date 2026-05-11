using Contry.Infrastructure.Authentication;

namespace Contry.Application.Tests;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public void CreateToken_ProducesOpaqueTokenAndDeterministicHash()
    {
        var service = new RefreshTokenService();

        var token = service.CreateToken();

        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.DoesNotContain("=", token.Token);
        Assert.Equal(service.HashToken(token.Token), token.TokenHash);
    }
}
