using Microsoft.Extensions.Hosting;

namespace Contry.Api.Features.KeepAlive;

public class KeepAliveService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<KeepAliveService> logger) : BackgroundService
{
    private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(13);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("KeepAliveService started. Will ping every {Interval}", _pingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pingInterval, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    await PerformPingAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("KeepAliveService stopping.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in KeepAliveService loop.");
            }
        }

        logger.LogInformation("KeepAliveService stopped.");
    }

    private async Task PerformPingAsync(CancellationToken cancellationToken)
    {
        // Render automatically injects RENDER_EXTERNAL_URL and overwrites .env files,
        // which causes issues if the onrender.com subdomain is blocked in favor of a custom domain.
        // We prioritize explicit custom domain variables.
        var baseUrl = configuration["KeepAliveUrl"] 
                      ?? configuration["BaseUrl"] 
                      ?? configuration["VITE_API_BASE_URL"] 
                      ?? configuration["RENDER_EXTERNAL_URL"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogDebug("No base URL configured for keep-alive ping.");
            return;
        }

        var url = $"{baseUrl.TrimEnd('/')}/ping";
        logger.LogInformation("Performing keep-alive ping at {Url}", url);

        try 
        {
            var client = httpClientFactory.CreateClient("KeepAlive");
            var response = await client.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Keep-alive ping successful - Status: {StatusCode}", response.StatusCode);
            }
            else
            {
                logger.LogWarning("Keep-alive ping returned non-success status - Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error during keep-alive ping.");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Keep-alive ping request timed out.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute keep-alive ping.");
        }
    }
}
