using Contry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contry.Api.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection _connection = null!;

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
                ["AuthCookies:AccessCookieName"] = "contry_access",
                ["AuthCookies:RefreshCookieName"] = "contry_refresh",
                ["AuthCookies:Path"] = "/",
                ["AuthCookies:SameSite"] = "Lax",
                ["AuthCookies:SecurePolicy"] = "None",
                ["Cors:AllowedOriginsCsv"] = "http://localhost:5173",
                ["AuthCleanup:IntervalMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ContryDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ContryDbContext>>();
            services.RemoveAll<ContryDbContext>();
            services.AddDbContext<ContryDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
