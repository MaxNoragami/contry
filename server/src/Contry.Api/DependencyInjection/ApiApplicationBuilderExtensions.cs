using Contry.Api.Common.Errors;
using Contry.Api.Common.Security;
using Contry.Api.Features.Auth;
using Contry.Api.Features.Datasets;
using Contry.Api.Features.Leaderboards;
using Contry.Api.Features.Ranked.Challenges;
using Contry.Api.Features.Ranked.Guesses;
using Contry.Api.Features.Ranked.Sessions;
using Contry.Api.Features.Ranked.Stats;
using Contry.Api.Features.TestRecords;
using Contry.Infrastructure.Datasets;
using Contry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contry.Api.DependencyInjection;

public static class ApiApplicationBuilderExtensions
{
    public static async Task UseContryApiAsync(this WebApplication app)
    {
        app.UseProblemDetailsExceptionMiddleware();
        await app.MigrateDatabaseAsync();
        await app.SyncBuiltInDatasetsAsync();
        await DatabaseSeeder.EnsureAdminUserAsync(app);

        if (app.Environment.IsDevelopment())
        {
            await DatabaseSeeder.SeedDevelopmentDataAsync(app);
            app.UseConfiguredSwaggerUi();
        }

        app.UseStaticFiles();
        app.UseHttpsRedirection();
        app.UseCors("Client");
        app.UseMiddleware<XsrfValidationMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
    }

    public static void MapContryEndpoints(this WebApplication app)
    {
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
        app.MapDatasetEndpoints();
        app.MapRankedChallengeEndpoints();
        app.MapRankedSessionEndpoints();
        app.MapRankedGuessEndpoints();
        app.MapRankedStatsEndpoints();
        app.MapLeaderboardEndpoints();
        app.MapTestRecordEndpoints();
    }

    private static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContryDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task SyncBuiltInDatasetsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var builtInDatasetCatalog = scope.ServiceProvider.GetRequiredService<BuiltInDatasetCatalog>();
        await builtInDatasetCatalog.SyncAsync(CancellationToken.None);
    }

    private static void UseConfiguredSwaggerUi(this WebApplication app)
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
}
