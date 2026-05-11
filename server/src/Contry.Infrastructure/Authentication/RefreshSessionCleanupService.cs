using Contry.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Contry.Infrastructure.Authentication;

public sealed class RefreshSessionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuthCleanupOptions> options,
    ILogger<RefreshSessionCleanupService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<RefreshSessionCleanupService> _logger = logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var authSessionService = scope.ServiceProvider.GetRequiredService<AuthSessionService>();
                await authSessionService.DeleteExpiredRefreshSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to clean expired refresh sessions.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
