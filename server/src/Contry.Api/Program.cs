using System.Text;
using Contry.Api.Common.Errors;
using Contry.Api.Features.TestRecords;
using Contry.Api.Common.OpenApi;
using Contry.Api.Features.Auth;
using Contry.Infrastructure;
using Contry.Infrastructure.Configuration;
using Contry.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

LoadRootEnvironmentFile(args);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddContryInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policyBuilder =>
    {
        var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        if (string.IsNullOrWhiteSpace(corsOptions.AllowedOriginsCsv))
        {
            corsOptions.AllowedOriginsCsv = builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty;
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
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

var app = builder.Build();

app.UseProblemDetailsExceptionMiddleware();

await EnsureDatabaseCreatedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    var swaggerIndexPath = Path.Combine(app.Environment.ContentRootPath, "Swagger", "index.html");
    app.Use(async (httpContext, next) =>
    {
        if (httpContext.Request.Path == "/swagger" || httpContext.Request.Path == "/swagger/")
        {
            httpContext.Response.Redirect("/swagger/index.html");
            return;
        }

        if (httpContext.Request.Path == "/swagger/index.html")
        {
            httpContext.Response.ContentType = "text/html; charset=utf-8";
            await httpContext.Response.SendFileAsync(swaggerIndexPath);
            return;
        }

        await next();
    });

    app.UseSwaggerUI(options =>
    {
        options.EnablePersistAuthorization();
        options.Interceptors.RequestInterceptorFunction = "function (req) { const method = (req.method || '').toUpperCase(); const needsXsrf = ['POST','PUT','PATCH','DELETE'].includes(method); if (!needsXsrf) { return req; } const authState = window.ui && window.ui.getState ? window.ui.getState().get('auth') : null; const authorized = authState && authState.get ? authState.get('authorized') : null; const xsrf = authorized && authorized.get ? authorized.get('xsrf') : null; const authValue = xsrf && xsrf.get ? xsrf.get('value') : null; const storedValue = window.localStorage.getItem('contry.swagger.xsrf'); const value = authValue || storedValue; if (value) { req.headers = req.headers || {}; req.headers['X-XSRF-TOKEN'] = value; } return req; }";
    });
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("Client");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => TypedResults.Ok(new
{
    name = "Contry API",
    status = "ok",
    docs = "/swagger"
}));

app.MapGet("/health", () => TypedResults.Ok(new
{
    status = "healthy"
}));

app.MapAuthEndpoints();
app.MapTestRecordEndpoints();

app.Run();

static async Task EnsureDatabaseCreatedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ContryDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await EnsureRefreshSessionSchemaAsync(dbContext);
    await EnsureTestRecordsTableExistsAsync(dbContext);
}

static async Task EnsureRefreshSessionSchemaAsync(ContryDbContext dbContext)
{
    var providerName = dbContext.Database.ProviderName ?? string.Empty;

    if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE refresh_sessions ADD COLUMN IF NOT EXISTS "SessionFamilyId" uuid;
            UPDATE refresh_sessions SET "SessionFamilyId" = "Id" WHERE "SessionFamilyId" IS NULL;
            ALTER TABLE refresh_sessions ALTER COLUMN "SessionFamilyId" SET NOT NULL;
            CREATE INDEX IF NOT EXISTS "IX_refresh_sessions_SessionFamilyId" ON refresh_sessions ("SessionFamilyId");
            """);

        return;
    }

    if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        if (!await SqliteColumnExistsAsync(dbContext, "refresh_sessions", "SessionFamilyId"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE refresh_sessions ADD COLUMN "SessionFamilyId" TEXT;
                """);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE refresh_sessions SET "SessionFamilyId" = "Id" WHERE "SessionFamilyId" IS NULL;
            CREATE INDEX IF NOT EXISTS "IX_refresh_sessions_SessionFamilyId" ON refresh_sessions ("SessionFamilyId");
            """);
    }
}

static async Task<bool> SqliteColumnExistsAsync(ContryDbContext dbContext, string tableName, string columnName)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureTestRecordsTableExistsAsync(ContryDbContext dbContext)
{
    var providerName = dbContext.Database.ProviderName ?? string.Empty;

    if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS test_records (
                "Id" uuid PRIMARY KEY,
                "UserId" uuid NOT NULL,
                "Name" character varying(128) NOT NULL,
                "Notes" character varying(2048) NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_test_records_UserId" ON test_records ("UserId");
            """);

        return;
    }

    if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS test_records (
                "Id" TEXT NOT NULL CONSTRAINT "PK_test_records" PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_test_records_UserId" ON test_records ("UserId");
            """);
    }
}

static void LoadRootEnvironmentFile(string[] args)
{
    var environmentName = GetEnvironmentName(args);
    var envFileName = environmentName switch
    {
        "Development" => ".env.dev",
        "Production" => ".env.prod",
        _ => $".env.{environmentName.ToLowerInvariant()}"
    };

    var envFilePath = FindEnvironmentFilePath(envFileName);

    if (envFilePath is null)
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(envFilePath, Encoding.UTF8))
    {
        var line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');

        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(key) || Environment.GetEnvironmentVariable(key) is not null)
        {
            continue;
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

static string? FindEnvironmentFilePath(string envFileName)
{
    var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
    var appBaseDirectory = new DirectoryInfo(AppContext.BaseDirectory);

    return FindInAncestors(currentDirectory, envFileName)
        ?? FindInAncestors(appBaseDirectory, envFileName);
}

static string? FindInAncestors(DirectoryInfo? directory, string envFileName)
{
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, envFileName);

        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}

static string GetEnvironmentName(string[] args)
{
    var aspnetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    if (!string.IsNullOrWhiteSpace(aspnetEnvironment))
    {
        return aspnetEnvironment;
    }

    var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

    if (!string.IsNullOrWhiteSpace(dotnetEnvironment))
    {
        return dotnetEnvironment;
    }

    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--environment" or "-e")
        {
            return args[i + 1];
        }
    }

    return "Production";
}

public partial class Program;
