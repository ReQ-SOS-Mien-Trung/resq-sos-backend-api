using RESQ.Application.Common.Models;

namespace RESQ.Application.Services;

public interface ISeedDataSyncService
{
    Task<SeedDataSyncReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default);
}
