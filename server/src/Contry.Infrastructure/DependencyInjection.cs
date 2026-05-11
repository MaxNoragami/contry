using System.Text;
using Contry.Application.Auth;
using Contry.Infrastructure.Authentication;
using Contry.Infrastructure.Configuration;
using Contry.Infrastructure.Persistence;
using Contry.Infrastructure.Xsrf;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Contry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddContryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
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

        services.AddDbContext<ContryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, IOptions<AuthCookieOptions>>((options, jwtOptionsAccessor, cookieOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                var cookieOptions = cookieOptionsAccessor.Value;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(cookieOptions.AccessCookieName, out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal;
                        var subject = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var role = principal?.FindFirstValue(ClaimTypes.Role) ?? principal?.FindFirstValue("role");
                        var jwtId = principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);

                        if (!Guid.TryParse(subject, out _) || string.IsNullOrWhiteSpace(jwtId) || role is not "USER" and not "ADMIN")
                        {
                            context.Fail("The JWT payload is missing or contains invalid required claims.");
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        var detail = string.IsNullOrWhiteSpace(context.ErrorDescription)
                            ? "The request requires a valid authenticated access token."
                            : context.ErrorDescription;

                        var problem = new ProblemDetails
                        {
                            Type = "/problems/auth/unauthorized",
                            Title = "Unauthorized.",
                            Status = StatusCodes.Status401Unauthorized,
                            Detail = detail,
                            Instance = context.Request.Path
                        };

                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";
                        await context.Response.WriteAsJsonAsync(problem);
                    },
                    OnForbidden = async context =>
                    {
                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        var problem = new ProblemDetails
                        {
                            Type = "/problems/auth/forbidden",
                            Title = "Forbidden.",
                            Status = StatusCodes.Status403Forbidden,
                            Detail = "The authenticated user is not allowed to access this resource.",
                            Instance = context.Request.Path
                        };

                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/problem+json";
                        await context.Response.WriteAsJsonAsync(problem);
                    }
                };
            });

        services.AddAuthorization();
        services.AddDataProtection();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IAccessTokenService, JwtAccessTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IXsrfTokenService, DataProtectionXsrfTokenService>();
        services.AddScoped<AuthCookieService>();
        services.AddScoped<AuthSessionService>();
        services.AddHostedService<RefreshSessionCleanupService>();

        return services;
    }
}
