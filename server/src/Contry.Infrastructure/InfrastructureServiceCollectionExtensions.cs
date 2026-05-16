using Contry.Infrastructure.Authentication;
using Contry.Infrastructure.Configuration;
using Contry.Infrastructure.Datasets;
using Contry.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Contry.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddContryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AdminBootstrapOptions>()
            .Bind(configuration.GetSection(AdminBootstrapOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Admin bootstrap username is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Email), "Admin bootstrap email is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "Admin bootstrap password is required.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Secret), "Jwt secret is required.")
            .ValidateOnStart();

        services.AddOptions<AuthCookieOptions>()
            .Bind(configuration.GetSection(AuthCookieOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.AllowedOriginsCsv))
                {
                    options.AllowedOriginsCsv = configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty;
                }
            })
            .ValidateOnStart();

        services.AddOptions<AuthCleanupOptions>()
            .Bind(configuration.GetSection(AuthCleanupOptions.SectionName))
            .ValidateOnStart();

        services.AddContryPersistence(configuration);
        services.AddContryAuthentication();
        services.AddScoped<BuiltInDatasetCatalog>();

        return services;
    }
}
