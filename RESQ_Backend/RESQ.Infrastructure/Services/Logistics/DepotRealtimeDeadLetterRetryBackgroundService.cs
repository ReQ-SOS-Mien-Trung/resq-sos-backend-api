using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Logistics;

public class DepotRealtimeDeadLetterRetryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<DepotRealtimeDeadLetterRetryBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<DepotRealtimeDeadLetterRetryBackgroundService> _logger = logger;

    private const int SweepIntervalMinutes = 10;
    private const int MaxDeadLetterReplayAttempts = 2;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Depot dead-letter retry service started");

        // Stagger startup to avoid connection pool exhaustion when all services start simultaneously
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ResQDbContext>();

                var cutoff = DateTime.UtcNow.AddMinutes(-15);
                var items = await dbContext.DepotRealtimeOutboxEvents
                    .Where(x => x.Status == "DeadLetter"
                                && x.UpdatedAt <= cutoff
                                && x.AttemptCount <= MaxDeadLetterReplayAttempts)
                    .OrderBy(x => x.UpdatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                if (items.Count > 0)
                {
                    var now = DateTime.UtcNow;
                    foreach (var item in items)
                    {
                        item.Status = "Pending";
                        item.NextAttemptAt = now;
                        item.LockOwner = null;
                        item.LockExpiresAt = null;
                        item.LastError = $"AutoReplay at {now:O}";
                        item.UpdatedAt = now;
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Requeued {Count} depot realtime dead-letter events", items.Count);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while replaying dead-letter events");
            }

            await Task.Delay(TimeSpan.FromMinutes(SweepIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("Depot dead-letter retry service stopped");
    }
}
