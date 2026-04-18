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

        // Lấy danh sách event Gathering đã quá deadline
        var expiredEventIds = await assemblyEventRepo.GetGatheringEventsWithExpiredDeadlineAsync(cancellationToken);

        if (expiredEventIds.Count == 0) return;

        logger.LogInformation(
            "Found {Count} assembly event(s) with expired check-in deadline. Auto-marking absent...",
            expiredEventIds.Count);

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
                logger.LogError(ex,
                    "Failed to auto-mark absent for assembly event #{EventId}.", eventId);
            }
        }
    }
}
