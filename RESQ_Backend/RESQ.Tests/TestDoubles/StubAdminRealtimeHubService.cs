using RESQ.Application.Common.Models;
using RESQ.Application.Services;

namespace RESQ.Tests.TestDoubles;

/// <summary>No-op stub for IAdminRealtimeHubService used in unit tests.</summary>
internal sealed class StubAdminRealtimeHubService : IAdminRealtimeHubService
{
    public Task PushFundingRequestUpdateAsync(AdminFundingRequestRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushCampaignUpdateAsync(AdminCampaignRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushDisbursementUpdateAsync(AdminDisbursementRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushRescuerApplicationUpdateAsync(AdminRescuerApplicationRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushDepotUpdateAsync(AdminDepotRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushDepotClosureUpdateAsync(AdminDepotClosureRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushTransferUpdateAsync(AdminTransferRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushSOSClusterUpdateAsync(AdminSOSClusterRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushMissionUpdateAsync(AdminMissionRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushMissionActivityUpdateAsync(AdminMissionActivityRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushRescueTeamUpdateAsync(AdminRescueTeamRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushSystemConfigUpdateAsync(AdminSystemConfigRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PushAiConfigUpdateAsync(AdminAiConfigRealtimeUpdate update, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
