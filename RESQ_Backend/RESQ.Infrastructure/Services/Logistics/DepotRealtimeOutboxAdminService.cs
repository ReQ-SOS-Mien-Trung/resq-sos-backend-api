using Microsoft.EntityFrameworkCore;
using RESQ.Application.Services;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Logistics;

public class DepotRealtimeOutboxAdminService(ResQDbContext dbContext) : IDepotRealtimeOutboxAdminService
{
    private readonly ResQDbContext _dbContext = dbContext;

    public async Task<int> ReplayDeadLettersAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default)
    {
        if (eventIds.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        var events = await _dbContext.DepotRealtimeOutboxEvents
            .Where(x => eventIds.Contains(x.Id) && x.Status == "DeadLetter")
            .ToListAsync(cancellationToken);

        foreach (var evt in events)
        {
            evt.Status = "Pending";
            evt.AttemptCount = 0;
            evt.NextAttemptAt = now;
            evt.LockOwner = null;
            evt.LockExpiresAt = null;
            evt.LastError = null;
            evt.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return events.Count;
    }
}
