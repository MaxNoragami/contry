namespace Contry.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "contry-api";

    public string Audience { get; set; } = "contry-client";

    public string Secret { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 1;

    public int RefreshTokenLifetimeMinutes { get; set; } = 5;
}
