using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Infrastructure.Services.Personnel;

/// <summary>
/// Background service tự động đánh dấu vắng mặt (Absent) cho những rescuer không check-in
/// khi sự kiện tập trung đã quá thời hạn check-in (CheckInDeadline).
/// Chạy mỗi 1 phút một lần.
/// </summary>
public class AssemblyCheckInDeadlineBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AssemblyCheckInDeadlineBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Assembly CheckIn Deadline Background Service started.");

        // Trì hoãn khởi động để tránh connection pool exhaustion khi nhiều service cùng start
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredCheckInDeadlinesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while auto-marking absent for expired assembly events.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        logger.LogInformation("Assembly CheckIn Deadline Background Service stopped.");
    }

    private async Task ProcessExpiredCheckInDeadlinesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var assemblyEventRepo = scope.ServiceProvider.GetRequiredService<IAssemblyEventRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // ── Bước 1: Scheduled → Gathering khi assemblyDate đã đến ──────────────
        var readyToGatherIds = await assemblyEventRepo.GetScheduledEventsReadyForGatheringAsync(cancellationToken);

        if (readyToGatherIds.Count > 0)
        {
            logger.LogInformation(
                "Found {Count} Scheduled assembly event(s) whose assemblyDate has passed. Transitioning to Gathering...",
                readyToGatherIds.Count);

            foreach (var eventId in readyToGatherIds)
            {
                try
                {
                    await assemblyEventRepo.StartGatheringAsync(eventId, cancellationToken);
                    await unitOfWork.SaveAsync();
                    logger.LogInformation("Assembly event #{EventId}: transitioned Scheduled → Gathering.", eventId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to transition assembly event #{EventId} to Gathering.", eventId);
                }
            }
        }

        // ── Bước 2: Auto-mark Absent khi CheckInDeadline đã hết ─────────────────
        var expiredEventIds = await assemblyEventRepo.GetGatheringEventsWithExpiredDeadlineAsync(cancellationToken);

        foreach (var eventId in expiredEventIds)
        {
            try
            {
                var markedCount = await assemblyEventRepo.AutoMarkAbsentForEventAsync(eventId, cancellationToken);
                await unitOfWork.SaveAsync();

                if (markedCount > 0)
                {
                    logger.LogInformation(
                        "Assembly event #{EventId}: auto-marked {Count} participant(s) as Absent (check-in deadline expired).",
                        eventId, markedCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to auto-mark absent for assembly event #{EventId}.", eventId);
            }
        }

        // ── Bước 3: Auto-complete sự kiện Gathering khi CheckInDeadline đã qua ──
        var toCompleteIds = await assemblyEventRepo.GetGatheringEventsExpiredAsync(cancellationToken);

        if (toCompleteIds.Count > 0)
        {
            logger.LogInformation(
                "Found {Count} Gathering assembly event(s) whose check-in deadline has passed. Auto-completing...",
                toCompleteIds.Count);

            foreach (var eventId in toCompleteIds)
            {
                try
                {
                    await assemblyEventRepo.CompleteEventAsync(eventId, cancellationToken);
                    await unitOfWork.SaveAsync();
                    logger.LogInformation("Assembly event #{EventId}: auto-completed (check-in deadline passed).", eventId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to auto-complete assembly event #{EventId}.", eventId);
                }
            }
        }
    }
}
