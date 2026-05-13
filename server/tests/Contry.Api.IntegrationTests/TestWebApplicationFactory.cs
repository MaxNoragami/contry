using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Contry.Api.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUsername = "admin_test";
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "admin-test-12345";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("contry_tests")
        .WithUsername("contry")
        .WithPassword("contry_tests123")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "contry-api-tests",
                ["Jwt:Audience"] = "contry-client-tests",
                ["Jwt:Secret"] = "integration-tests-secret-key-1234567890",
                ["Jwt:AccessTokenLifetimeMinutes"] = "1",
                ["Jwt:RefreshTokenLifetimeMinutes"] = "5",
                ["ConnectionStrings:Database"] = _database.GetConnectionString(),
                ["AdminBootstrap:Username"] = AdminUsername,
                ["AdminBootstrap:Email"] = AdminEmail,
                ["AdminBootstrap:Password"] = AdminPassword,
                ["AuthCookies:AccessCookieName"] = "contry_access",
                ["AuthCookies:RefreshCookieName"] = "contry_refresh",
                ["AuthCookies:Path"] = "/",
                ["AuthCookies:SameSite"] = "Lax",
                ["AuthCookies:SecurePolicy"] = "None",
                ["Cors:AllowedOriginsCsv"] = "http://localhost:5173",
                ["AuthCleanup:IntervalMinutes"] = "60"
            });
        });
    }

    public Task InitializeAsync() => _database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _database.DisposeAsync().AsTask();
}
