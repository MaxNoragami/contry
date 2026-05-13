using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Contry.Application.Auth;
using Contry.Application.Ranked;
using Contry.Infrastructure.Datasets;
using Contry.Infrastructure.Ranked;
using Contry.Infrastructure.Persistence;
using Contry.Infrastructure.Xsrf;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Contry.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddContryAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<Configuration.JwtOptions>, IOptions<Configuration.AuthCookieOptions>>(ConfigureJwtBearerOptions);

        services.AddAuthorization();
        services.AddDataProtection();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IAuthSessionOptions, AuthSessionOptions>();
        services.AddScoped<IAuthStore, AuthStore>();
        services.AddScoped<IRankedStore, RankedStore>();
        services.AddScoped<AuthSessionIssuer>();
        services.AddScoped<RegisterUserCommandHandler>();
        services.AddScoped<CreateSessionCommandHandler>();
        services.AddScoped<RefreshSessionCommandHandler>();
        services.AddScoped<RevokeCurrentSessionCommandHandler>();
        services.AddScoped<GetCurrentUserQueryHandler>();
        services.AddScoped<GetCurrentRankedChallengeQueryHandler>();
        services.AddScoped<GetCurrentRankedSessionQueryHandler>();
        services.AddScoped<CreateRankedGuessCommandHandler>();
        services.AddScoped<RankedGuessEvaluator>();
        services.AddScoped<IAccessTokenService, JwtAccessTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IXsrfTokenService, DataProtectionXsrfTokenService>();
        services.AddScoped<IRankedDatasetProvider, FileRankedDatasetProvider>();
        services.AddScoped<AuthCookieService>();
        services.AddScoped<CurrentRefreshSessionService>();
        services.AddHostedService<RefreshSessionCleanupService>();

        return services;
    }

    private static void ConfigureJwtBearerOptions(
        JwtBearerOptions options,
        IOptions<Configuration.JwtOptions> jwtOptionsAccessor,
        IOptions<Configuration.AuthCookieOptions> cookieOptionsAccessor)
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
            RoleClaimType = ClaimTypes.Role
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
    }
}
