using RESQ.Application.Common.Models;

namespace RESQ.Application.Services;

public interface IAdminRealtimeHubService
{
    Task PushFundingRequestUpdateAsync(
        AdminFundingRequestRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushCampaignUpdateAsync(
        AdminCampaignRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushDisbursementUpdateAsync(
        AdminDisbursementRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushRescuerApplicationUpdateAsync(
        AdminRescuerApplicationRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushDepotUpdateAsync(
        AdminDepotRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushDepotClosureUpdateAsync(
        AdminDepotClosureRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushTransferUpdateAsync(
        AdminTransferRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushSOSClusterUpdateAsync(
        AdminSOSClusterRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushMissionUpdateAsync(
        AdminMissionRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushMissionActivityUpdateAsync(
        AdminMissionActivityRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushRescueTeamUpdateAsync(
        AdminRescueTeamRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushSystemConfigUpdateAsync(
        AdminSystemConfigRealtimeUpdate update,
        CancellationToken cancellationToken = default);

    Task PushAiConfigUpdateAsync(
        AdminAiConfigRealtimeUpdate update,
        CancellationToken cancellationToken = default);
}
