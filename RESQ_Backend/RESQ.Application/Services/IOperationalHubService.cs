using RESQ.Application.Common.Models;

namespace RESQ.Application.Services;

public interface IOperationalHubService
{
    Task PushAssemblyPointListUpdateAsync(CancellationToken cancellationToken = default);

    Task PushDepotInventoryUpdateAsync(
        int depotId,
        string operation,
        CancellationToken cancellationToken = default);

    Task PushLogisticsUpdateAsync(
        string resourceType,
        int? clusterId = null,
        CancellationToken cancellationToken = default);

    Task PushSupplyRequestUpdateAsync(
        SupplyRequestRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushDepotActivityUpdateAsync(
        DepotActivityRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushDepotClosureUpdateAsync(
        DepotClosureRealtimeUpdate update,
        CancellationToken cancellationToken = default);
}
