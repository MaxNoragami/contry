using Microsoft.Extensions.Hosting;

namespace Contry.Api.Features.KeepAlive;

public class KeepAliveService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<KeepAliveService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(13), stoppingToken);
            
            try 
            {
                var baseUrl = configuration["RENDER_EXTERNAL_URL"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    logger.LogDebug("RENDER_EXTERNAL_URL is not set, skipping keep-alive ping.");
                    continue;
                }

                var url = $"{baseUrl.TrimEnd('/')}/ping";
                var client = httpClientFactory.CreateClient("KeepAlive");
                var response = await client.GetAsync(url, stoppingToken);
                
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Successfully pinged {url} to keep server awake.", url);
                }
                else
                {
                    logger.LogWarning("Ping to {url} failed with status code {statusCode}.", url, response.StatusCode);
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Failed to execute keep-alive ping.");
            }
        }
    }
}
