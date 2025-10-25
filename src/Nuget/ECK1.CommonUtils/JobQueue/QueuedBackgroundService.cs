using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECK1.CommonUtils.JobQueue;

public class QueuedBackgroundService : BackgroundService
{
    private readonly ILogger<QueuedBackgroundService> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _services;

    public QueuedBackgroundService(
        IBackgroundTaskQueue taskQueue,
        IServiceProvider services,
        ILogger<QueuedBackgroundService> logger)
    {
        _taskQueue = taskQueue;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background task processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _services.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background task processing.");
            }
        }
    }
}