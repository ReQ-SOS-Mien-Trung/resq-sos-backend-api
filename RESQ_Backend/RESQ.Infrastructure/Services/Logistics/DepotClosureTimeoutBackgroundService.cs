using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Infrastructure.Services.Logistics;

/// <summary>
/// Background daemon that sweeps depot closures that have exceeded their 30-minute timeout.
/// Runs every 5 minutes.
/// For each timed-out closure it:
///   1. Atomically claims the record (InProgress → Processing).
///   2. Restores the depot to its previous status (Available).
///   3. Marks the closure as TimedOut.
///   4. Sends a Firebase push notification to the admin who initiated the closure.
/// </summary>
public class DepotClosureTimeoutBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<DepotClosureTimeoutBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<DepotClosureTimeoutBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DepotClosureTimeoutBackgroundService started.");

        // Stagger startup to avoid connection pool exhaustion when all services start simultaneously
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTimedOutClosuresAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing timed-out depot closures.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        _logger.LogInformation("DepotClosureTimeoutBackgroundService stopped.");
    }

    private async Task ProcessTimedOutClosuresAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var closureRepo = scope.ServiceProvider.GetRequiredService<IDepotClosureRepository>();
        var depotRepo   = scope.ServiceProvider.GetRequiredService<IDepotRepository>();
        var unitOfWork  = scope.ServiceProvider.GetRequiredService<RESQ.Application.Repositories.Base.IUnitOfWork>();
        var firebase    = scope.ServiceProvider.GetRequiredService<IFirebaseService>();

        var timedOut = await closureRepo.GetTimedOutClosuresAsync(cancellationToken);

        foreach (var closure in timedOut)
        {
            // Atomic claim — prevents race with concurrent admin resolve
            var claimed = await closureRepo.TryClaimForProcessingAsync(closure.Id, cancellationToken);
            if (!claimed)
            {
                _logger.LogDebug("Closure #{ClosureId} already claimed by another process — skipping.", closure.Id);
                continue;
            }

            try
            {
                await unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var depot = await depotRepo.GetByIdAsync(closure.DepotId, cancellationToken);
                    if (depot == null)
                    {
                        _logger.LogWarning("Depot #{DepotId} not found when timing out closure #{ClosureId}.", closure.DepotId, closure.Id);
                        closure.MarkTimedOut();
                        await closureRepo.UpdateAsync(closure, cancellationToken);
                        return;
                    }

                    if (depot.Status == DepotStatus.Unavailable)
                        depot.RestoreFromClosing(closure.PreviousStatus);

                    closure.MarkTimedOut();

                    await depotRepo.UpdateAsync(depot, cancellationToken);
                    await closureRepo.UpdateAsync(closure, cancellationToken);
                    await unitOfWork.SaveAsync();
                });

                _logger.LogInformation(
                    "Depot closure #{ClosureId} for depot #{DepotId} timed out — depot restored to {PreviousStatus}.",
                    closure.Id, closure.DepotId, closure.PreviousStatus);

                // Notify the admin who initiated the closure
                try
                {
                    await firebase.SendNotificationToUserAsync(
                        closure.InitiatedBy,
                        "Quy trình đóng kho đã hết thời gian",
                        $"Kho #{closure.DepotId} đã được khôi phục về trạng thái ban đầu vì quá thời gian xử lý (30 phút). " +
                        $"Vui lòng thực hiện lại nếu muốn tiếp tục đóng kho.",
                        "depot_closure_timeout",
                        cancellationToken);
                }
                catch (Exception notifyEx)
                {
                    // Notification failure must not abort the timeout processing
                    _logger.LogWarning(notifyEx, "Failed to send timeout notification for closure #{ClosureId}.", closure.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process timeout for closure #{ClosureId}.", closure.Id);
            }
        }
    }
}
