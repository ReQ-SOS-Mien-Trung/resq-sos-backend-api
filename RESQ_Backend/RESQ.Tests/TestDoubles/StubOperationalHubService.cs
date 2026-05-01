using RESQ.Application.Common.Models;
using RESQ.Application.Services;

namespace RESQ.Tests.TestDoubles;

/// <summary>No-op stub for IOperationalHubService used in unit tests.</summary>
internal sealed class StubOperationalHubService : IOperationalHubService
{
    public Task PushAssemblyPointListUpdateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PushAssemblyEventCheckedInRescuersUpdateAsync(
        int eventId,
        string operation,
        Guid? rescuerId = null,
        CancellationToken cancellationToken = default,
        int? assemblyPointId = null)
        => Task.CompletedTask;

    public Task PushDepotInventoryUpdateAsync(int depotId, string operation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PushInventoryLotsUpdateAsync(int depotId, int itemModelId, string operation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PushLogisticsUpdateAsync(string resourceType, int? clusterId = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PushSupplyRequestUpdateAsync(SupplyRequestRealtimeUpdate update, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PushDepotActivityUpdateAsync(DepotActivityRealtimeUpdate update, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PushDepotClosureUpdateAsync(DepotClosureRealtimeUpdate update, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
