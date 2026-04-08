using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Infrastructure.Entities.Notifications;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Logistics;

public class DepotRealtimeOutboxDispatcherBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<DepotRealtimeOutboxDispatcherBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<DepotRealtimeOutboxDispatcherBackgroundService> _logger = logger;

    private const int PollIntervalMs = 1500;
    private const int BatchSize = 100;
    private const int MaxAttempts = 8;
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Depot realtime outbox dispatcher started");

        // Stagger startup to avoid connection pool exhaustion when all services start simultaneously
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ResQDbContext>();

                var dueDepotIds = await dbContext.DepotRealtimeOutboxEvents
                    .Where(x => (x.Status == "Pending" || x.Status == "Failed")
                                && x.NextAttemptAt <= DateTime.UtcNow
                                && (x.LockExpiresAt == null || x.LockExpiresAt < DateTime.UtcNow))
                    .Select(x => x.DepotId)
                    .Distinct()
                    .OrderBy(x => x)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                if (dueDepotIds.Count == 0)
                {
                    await Task.Delay(PollIntervalMs, stoppingToken);
                    continue;
                }

                foreach (var depotId in dueDepotIds)
                {
                    await ProcessPartitionAsync(scope.ServiceProvider, depotId, stoppingToken);
                }

                await Task.Delay(PollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in depot realtime outbox dispatcher loop");
                await Task.Delay(PollIntervalMs, stoppingToken);
            }
        }

        _logger.LogInformation("Depot realtime outbox dispatcher stopped");
    }

    private async Task ProcessPartitionAsync(IServiceProvider serviceProvider, int depotId, CancellationToken cancellationToken)
    {
        var dbContext = serviceProvider.GetRequiredService<ResQDbContext>();
        var notificationHub = serviceProvider.GetRequiredService<INotificationHubService>();
        var depotRepository = serviceProvider.GetRequiredService<IDepotRepository>();
        var lockKey = BuildAdvisoryLockKey(depotId);

        if (!await TryAcquireAdvisoryLockAsync(dbContext, lockKey, cancellationToken))
            return;

        var lockOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var claimed = await dbContext.DepotRealtimeOutboxEvents
                    .Where(x => x.DepotId == depotId
                                && (x.Status == "Pending" || x.Status == "Failed")
                                && x.NextAttemptAt <= now
                                && (x.LockExpiresAt == null || x.LockExpiresAt < now))
                    .OrderBy(x => x.Version)
                    .ThenBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

                if (claimed.Count == 0)
                    break;

                var lockExpires = now.Add(LockTtl);
                foreach (var evt in claimed)
                {
                    evt.Status = "Processing";
                    evt.LockOwner = lockOwner;
                    evt.LockExpiresAt = lockExpires;
                    evt.UpdatedAt = now;
                }
                await dbContext.SaveChangesAsync(cancellationToken);

                var ordered = claimed.OrderBy(x => x.Version).ThenBy(x => x.Id).ToList();
                var dispatchPlan = BuildDispatchPlan(ordered);

                foreach (var item in dispatchPlan)
                {
                    if (!item.ShouldDispatch)
                    {
                        item.Event.Status = "Dispatched";
                        item.Event.ProcessedAt = DateTime.UtcNow;
                        item.Event.LockOwner = null;
                        item.Event.LockExpiresAt = null;
                        item.Event.LastError = "Coalesced(delta non-critical)";
                        item.Event.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }

                    try
                    {
                        var payload = await BuildPayloadAsync(item.Event, depotRepository, cancellationToken);
                        var envelope = new DepotRealtimeEventEnvelope
                        {
                            EventId = item.Event.Id,
                            EventType = item.Event.EventType,
                            DepotId = item.Event.DepotId,
                            MissionId = item.Event.MissionId,
                            Version = item.Event.Version,
                            OccurredAtUtc = item.Event.OccurredAt,
                            Operation = item.Event.Operation,
                            PayloadKind = item.Event.PayloadKind,
                            IsCritical = item.Event.IsCritical,
                            RequeryRecommended = true,
                            Payload = payload
                        };

                        var group = DepotRealtimeGroupKey.Build(item.Event.MissionId, item.Event.DepotId);
                        await notificationHub.SendToGroupAsync(group, "DepotUpdated", envelope, cancellationToken);

                        item.Event.Status = "Dispatched";
                        item.Event.ProcessedAt = DateTime.UtcNow;
                        item.Event.LockOwner = null;
                        item.Event.LockExpiresAt = null;
                        item.Event.LastError = null;
                        item.Event.UpdatedAt = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        item.Event.AttemptCount += 1;
                        item.Event.LockOwner = null;
                        item.Event.LockExpiresAt = null;
                        item.Event.UpdatedAt = DateTime.UtcNow;

                        if (item.Event.AttemptCount >= MaxAttempts)
                        {
                            item.Event.Status = "DeadLetter";
                            item.Event.LastError = ex.Message;
                        }
                        else
                        {
                            item.Event.Status = "Failed";
                            item.Event.LastError = ex.Message;
                            item.Event.NextAttemptAt = DateTime.UtcNow.Add(GetBackoff(item.Event.AttemptCount));
                        }
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            await ReleaseAdvisoryLockAsync(dbContext, lockKey, cancellationToken);
        }
    }

    private static List<DispatchPlanItem> BuildDispatchPlan(IReadOnlyList<DepotRealtimeOutbox> orderedEvents)
    {
        var plan = new List<DispatchPlanItem>(orderedEvents.Count);
        var deltaBuffer = new List<DepotRealtimeOutbox>();

        void FlushDeltaBuffer()
        {
            if (deltaBuffer.Count == 0) return;

            for (var i = 0; i < deltaBuffer.Count - 1; i++)
            {
                plan.Add(new DispatchPlanItem(deltaBuffer[i], false));
            }

            plan.Add(new DispatchPlanItem(deltaBuffer[^1], true));
            deltaBuffer.Clear();
        }

        foreach (var evt in orderedEvents)
        {
            var isDeltaNonCritical = evt.PayloadKind == "Delta" && !evt.IsCritical;
            if (isDeltaNonCritical)
            {
                deltaBuffer.Add(evt);
                continue;
            }

            FlushDeltaBuffer();
            plan.Add(new DispatchPlanItem(evt, true));
        }

        FlushDeltaBuffer();
        return plan;
    }

    private static async Task<object?> BuildPayloadAsync(
        DepotRealtimeOutbox evt,
        IDepotRepository depotRepository,
        CancellationToken cancellationToken)
    {
        var canUseSnapshot = evt.PayloadKind == "Delta"
                             && !evt.IsCritical
                             && !string.IsNullOrWhiteSpace(evt.SnapshotPayload);

        if (canUseSnapshot)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(evt.SnapshotPayload!);
            }
            catch
            {
                // Fallback to re-query.
            }
        }

        var depot = await depotRepository.GetByIdAsync(evt.DepotId, cancellationToken);
        if (depot == null)
        {
            return new
            {
                Id = evt.DepotId,
                Deleted = true
            };
        }

        var manager = depot.CurrentManager;
        return new
        {
            Id = depot.Id,
            Name = depot.Name,
            Address = depot.Address,
            Latitude = depot.Location?.Latitude,
            Longitude = depot.Location?.Longitude,
            Capacity = depot.Capacity,
            CurrentUtilization = depot.CurrentUtilization,
            Status = depot.Status.ToString(),
            Manager = manager != null
                ? new
                {
                    Id = manager.UserId,
                    FirstName = manager.FirstName,
                    LastName = manager.LastName,
                    Email = manager.Email,
                    Phone = manager.Phone
                }
                : null,
            LastUpdatedAt = depot.LastUpdatedAt
        };
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        var seconds = Math.Min(60, Math.Pow(2, attempt));
        var jitterMs = Random.Shared.Next(100, 700);
        return TimeSpan.FromSeconds(seconds).Add(TimeSpan.FromMilliseconds(jitterMs));
    }

    private static long BuildAdvisoryLockKey(int depotId)
    {
        return 10_000_000L + depotId;
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(ResQDbContext dbContext, long lockKey, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool locked && locked;
    }

    private static async Task ReleaseAdvisoryLockAsync(ResQDbContext dbContext, long lockKey, CancellationToken cancellationToken)
    {
        if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
            return;

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@key)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);

        await command.ExecuteScalarAsync(cancellationToken);
        await dbContext.Database.CloseConnectionAsync();
    }

    private sealed record DispatchPlanItem(DepotRealtimeOutbox Event, bool ShouldDispatch);
}
