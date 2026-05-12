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
        var binding = new XsrfSessionBinding(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1));

        var token = service.CreateToken(binding);

        var valid = service.TryValidateToken(token.Token, binding, out var expiresAtUtc);
        var invalid = service.TryValidateToken(token.Token, binding with { SessionFamilyId = Guid.NewGuid() }, out _);

        Assert.True(valid);
        Assert.Equal(binding.ExpiresAtUtc, expiresAtUtc);
        Assert.False(invalid);
    }
}
