using Contry.Api.Common.OpenApi;
using Contry.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.OpenApi;

namespace Contry.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddContryApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblyContaining<Program>();
        services.AddContryCors(configuration);
        services.AddContrySwagger();
        services.AddEndpointsApiExplorer();

        return services;
    }

    private static IServiceCollection AddContryCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("Client", policyBuilder =>
            {
                var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

                if (string.IsNullOrWhiteSpace(corsOptions.AllowedOriginsCsv))
                {
                    corsOptions.AllowedOriginsCsv = configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty;
                }

                var allowedOrigins = corsOptions.GetAllowedOrigins();

                if (allowedOrigins.Length > 0)
                {
                    policyBuilder.WithOrigins(allowedOrigins);
                }

                policyBuilder.AllowAnyHeader();
                policyBuilder.AllowAnyMethod();
                policyBuilder.AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddContrySwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Contry API",
                Version = "v1",
                Description = "Cookie-first auth API for the Contry backend. Login is performed through /sessions, and unsafe cookie-authenticated requests require X-XSRF-TOKEN."
            });

            options.AddSecurityDefinition("xsrf", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-XSRF-TOKEN",
                Description = "Signed XSRF token returned by GET /xsrf and required for unsafe cookie-authenticated requests."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("xsrf")
                {
                }] = []
            });

            options.OperationFilter<XsrfOperationFilter>();
        });

        return services;
    }
}
