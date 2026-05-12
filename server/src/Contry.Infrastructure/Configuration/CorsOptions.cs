namespace Contry.Infrastructure.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string AllowedOriginsCsv { get; set; } = string.Empty;

    public string[] GetAllowedOrigins() => AllowedOriginsCsv
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
