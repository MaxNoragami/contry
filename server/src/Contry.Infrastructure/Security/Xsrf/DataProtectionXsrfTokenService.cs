using System.Text;
using System.Text.Json;
using Contry.Application.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace Contry.Infrastructure.Xsrf;

public sealed class DataProtectionXsrfTokenService(IDataProtectionProvider provider) : IXsrfTokenService
{
    private readonly IDataProtector _protector = provider.CreateProtector("Contry.Api.XsrfToken.v1");

    public XsrfTokenResult CreateToken(XsrfSessionBinding binding)
    {
        var payload = new XsrfPayload(binding.UserId, binding.SessionFamilyId, binding.ExpiresAtUtc);
        var json = JsonSerializer.Serialize(payload);
        var protectedPayload = _protector.Protect(Encoding.UTF8.GetBytes(json));
        var token = Convert.ToBase64String(protectedPayload)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new XsrfTokenResult(token, binding.ExpiresAtUtc);
    }

    public bool TryValidateToken(string token, XsrfSessionBinding binding, out DateTimeOffset expiresAtUtc)
    {
        expiresAtUtc = default;

        try
        {
            var protectedBytes = Convert.FromBase64String(PadBase64(token.Replace('-', '+').Replace('_', '/')));
            var jsonBytes = _protector.Unprotect(protectedBytes);
            var payload = JsonSerializer.Deserialize<XsrfPayload>(jsonBytes);

            if (payload is null || payload.UserId != binding.UserId || payload.SessionFamilyId != binding.SessionFamilyId)
            {
                return false;
            }

            expiresAtUtc = payload.ExpiresAtUtc;
            return payload.ExpiresAtUtc > DateTimeOffset.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static string PadBase64(string input)
        => (input.Length % 4) switch
        {
            2 => input + "==",
            3 => input + "=",
            _ => input
        };

    private sealed record XsrfPayload(Guid UserId, Guid SessionFamilyId, DateTimeOffset ExpiresAtUtc);
}
