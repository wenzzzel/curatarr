namespace Curatarr.Services.Scheduling;

public sealed class ScheduledTaskHostedService(
    ScheduledTaskRegistry registry,
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledTaskHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var task in registry.Tasks)
            {
                if (task.IsRunning || DateTimeOffset.UtcNow < task.NextRun) continue;

                using var scope = scopeFactory.CreateScope();
                try
                {
                    await task.RunAsync(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Scheduled task '{Name}' threw", task.Name);
                }
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
