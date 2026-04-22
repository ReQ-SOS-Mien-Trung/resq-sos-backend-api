using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

public sealed class AdminRealtimeHubService(
    IHubContext<AdminFinanceHub> adminFinanceHubContext,
    IHubContext<AdminIdentityHub> adminIdentityHubContext,
    IHubContext<AdminOperationsHub> adminOperationsHubContext,
    IHubContext<AdminSystemHub> adminSystemHubContext,
    ILogger<AdminRealtimeHubService> logger) : IAdminRealtimeHubService
{
    private readonly IHubContext<AdminFinanceHub> _adminFinanceHubContext = adminFinanceHubContext;
    private readonly IHubContext<AdminIdentityHub> _adminIdentityHubContext = adminIdentityHubContext;
    private readonly IHubContext<AdminOperationsHub> _adminOperationsHubContext = adminOperationsHubContext;
    private readonly IHubContext<AdminSystemHub> _adminSystemHubContext = adminSystemHubContext;
    private readonly ILogger<AdminRealtimeHubService> _logger = logger;

    private const string FundingRequestEvent = "ReceiveFundingRequestUpdate";
    private const string CampaignEvent = "ReceiveCampaignUpdate";
    private const string DisbursementEvent = "ReceiveDisbursementUpdate";
    private const string RescuerApplicationEvent = "ReceiveRescuerApplicationUpdate";
    private const string DepotEvent = "ReceiveDepotUpdate";
    private const string DepotClosureEvent = "ReceiveDepotClosureUpdate";
    private const string TransferEvent = "ReceiveTransferUpdate";
    private const string SOSClusterEvent = "ReceiveSOSClusterUpdate";
    private const string MissionEvent = "ReceiveMissionUpdate";
    private const string MissionActivityEvent = "ReceiveMissionActivityUpdate";
    private const string RescueTeamEvent = "ReceiveRescueTeamUpdate";
    private const string SystemConfigEvent = "ReceiveSystemConfigUpdate";
    private const string AiConfigEvent = "ReceiveAiConfigUpdate";

    public async Task PushFundingRequestUpdateAsync(
        AdminFundingRequestRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminFinanceHub.FundingRequestsGroup,
                AdminFinanceHub.FundingRequestGroup(update.RequestId)
            };

            await SendToFinanceGroupsAsync(groups, FundingRequestEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminFinanceHub] Failed to push {Event} for FundingRequestId={FundingRequestId}",
                FundingRequestEvent,
                update.RequestId);
        }
    }

    public async Task PushCampaignUpdateAsync(
        AdminCampaignRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminFinanceHub.CampaignsGroup,
                AdminFinanceHub.CampaignGroup(update.CampaignId),
                AdminFinanceHub.CampaignFundFlowGroup(update.CampaignId)
            };

            await SendToFinanceGroupsAsync(groups, CampaignEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminFinanceHub] Failed to push {Event} for CampaignId={CampaignId}",
                CampaignEvent,
                update.CampaignId);
        }
    }

    public async Task PushDisbursementUpdateAsync(
        AdminDisbursementRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminFinanceHub.DisbursementsGroup
            };

            if (update.CampaignId.HasValue)
            {
                groups.Add(AdminFinanceHub.CampaignGroup(update.CampaignId.Value));
                groups.Add(AdminFinanceHub.CampaignFundFlowGroup(update.CampaignId.Value));
            }

            await SendToFinanceGroupsAsync(groups, DisbursementEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminFinanceHub] Failed to push {Event} for DisbursementId={DisbursementId}",
                DisbursementEvent,
                update.DisbursementId);
        }
    }

    public async Task PushRescuerApplicationUpdateAsync(
        AdminRescuerApplicationRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminIdentityHub.RescuerApplicationsGroup,
                AdminIdentityHub.RescuerApplicationGroup(update.ApplicationId)
            };

            await SendToIdentityGroupsAsync(groups, RescuerApplicationEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminIdentityHub] Failed to push {Event} for ApplicationId={ApplicationId}",
                RescuerApplicationEvent,
                update.ApplicationId);
        }
    }

    public async Task PushDepotUpdateAsync(
        AdminDepotRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminOperationsHub.DepotsGroup
            };

            if (update.DepotId.HasValue)
                groups.Add(AdminOperationsHub.DepotGroup(update.DepotId.Value));

            await SendToOperationsGroupsAsync(groups, DepotEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminOperationsHub] Failed to push {Event} for DepotId={DepotId}",
                DepotEvent,
                update.DepotId);
        }
    }

    public async Task PushDepotClosureUpdateAsync(
        AdminDepotClosureRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminOperationsHub.DepotsGroup
            };

            if (update.SourceDepotId.HasValue)
                groups.Add(AdminOperationsHub.DepotGroup(update.SourceDepotId.Value));

            if (update.TargetDepotId.HasValue)
                groups.Add(AdminOperationsHub.DepotGroup(update.TargetDepotId.Value));

            if (update.ClosureId.HasValue)
                groups.Add(AdminOperationsHub.DepotClosureGroup(update.ClosureId.Value));

            await SendToOperationsGroupsAsync(groups, DepotClosureEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminOperationsHub] Failed to push {Event} for ClosureId={ClosureId}",
                DepotClosureEvent,
                update.ClosureId);
        }
    }

    public async Task PushTransferUpdateAsync(
        AdminTransferRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminOperationsHub.DepotsGroup,
                AdminOperationsHub.DepotGroup(update.SourceDepotId),
                AdminOperationsHub.DepotGroup(update.TargetDepotId),
                AdminOperationsHub.TransferGroup(update.TransferId)
            };

            if (update.ClosureId.HasValue)
                groups.Add(AdminOperationsHub.DepotClosureGroup(update.ClosureId.Value));

            await SendToOperationsGroupsAsync(groups, TransferEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminOperationsHub] Failed to push {Event} for TransferId={TransferId}",
                TransferEvent,
                update.TransferId);
        }
    }

    public async Task PushSOSClusterUpdateAsync(
        AdminSOSClusterRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminOperationsHub.SOSClustersGroup
            };

            if (update.ClusterId.HasValue)
                groups.Add(AdminOperationsHub.SOSClusterGroup(update.ClusterId.Value));

            await SendToOperationsGroupsAsync(groups, SOSClusterEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminOperationsHub] Failed to push {Event} for ClusterId={ClusterId}",
                SOSClusterEvent,
                update.ClusterId);
        }
    }

    public async Task PushMissionUpdateAsync(
        AdminMissionRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminOperationsHub.SOSClustersGroup,
                AdminOperationsHub.RescueTeamsGroup,
                AdminOperationsHub.MissionGroup(update.MissionId),
                AdminOperationsHub.MissionActivitiesGroup(update.MissionId)
            };

            if (update.ClusterId.HasValue)
                groups.Add(AdminOperationsHub.SOSClusterGroup(update.ClusterId.Value));

            await SendToOperationsGroupsAsync(groups, MissionEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminOperationsHub] Failed to push {Event} for MissionId={MissionId}",
                MissionEvent,
                update.MissionId);
        }
    }

    public async Task PushMissionActivityUpdateAsync(
        AdminMissionActivityRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminOperationsHub.SOSClustersGroup,
                AdminOperationsHub.RescueTeamsGroup
            };

            if (update.MissionId.HasValue)
            {
                groups.Add(AdminOperationsHub.MissionGroup(update.MissionId.Value));
                groups.Add(AdminOperationsHub.MissionActivitiesGroup(update.MissionId.Value));
            }

            if (update.DepotId.HasValue)
                groups.Add(AdminOperationsHub.DepotGroup(update.DepotId.Value));

            await SendToOperationsGroupsAsync(groups, MissionActivityEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminOperationsHub] Failed to push {Event} for ActivityId={ActivityId}",
                MissionActivityEvent,
                update.ActivityId);
        }
    }

    public async Task PushRescueTeamUpdateAsync(
        AdminRescueTeamRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                AdminOperationsHub.RescueTeamsGroup
            };

            if (update.TeamId.HasValue)
                groups.Add(AdminOperationsHub.RescueTeamGroup(update.TeamId.Value));

            await SendToOperationsGroupsAsync(groups, RescueTeamEvent, update, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminOperationsHub] Failed to push {Event} for TeamId={TeamId}",
                RescueTeamEvent,
                update.TeamId);
        }
    }

    public async Task PushSystemConfigUpdateAsync(
        AdminSystemConfigRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            await SendToSystemGroupsAsync(
                [AdminSystemHub.SystemConfigsGroup],
                SystemConfigEvent,
                update,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminSystemHub] Failed to push {Event} for EntityType={EntityType} EntityId={EntityId}",
                SystemConfigEvent,
                update.EntityType,
                update.EntityId);
        }
    }

    public async Task PushAiConfigUpdateAsync(
        AdminAiConfigRealtimeUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            update.ChangedAt = NormalizeChangedAt(update.ChangedAt);
            await SendToSystemGroupsAsync(
                [AdminSystemHub.AiConfigsGroup],
                AiConfigEvent,
                update,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AdminSystemHub] Failed to push {Event} for EntityType={EntityType} EntityId={EntityId}",
                AiConfigEvent,
                update.EntityType,
                update.EntityId);
        }
    }

    private async Task SendToFinanceGroupsAsync(
        IEnumerable<string> groups,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var tasks = groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => _adminFinanceHubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken))
            .ToList();

        if (tasks.Count == 0)
            return;

        await Task.WhenAll(tasks);
    }

    private async Task SendToIdentityGroupsAsync(
        IEnumerable<string> groups,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var tasks = groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => _adminIdentityHubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken))
            .ToList();

        if (tasks.Count == 0)
            return;

        await Task.WhenAll(tasks);
    }

    private async Task SendToOperationsGroupsAsync(
        IEnumerable<string> groups,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var tasks = groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => _adminOperationsHubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken))
            .ToList();

        if (tasks.Count == 0)
            return;

        await Task.WhenAll(tasks);
    }

    private async Task SendToSystemGroupsAsync(
        IEnumerable<string> groups,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var tasks = groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => _adminSystemHubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken))
            .ToList();

        if (tasks.Count == 0)
            return;

        await Task.WhenAll(tasks);
    }

    private static DateTime NormalizeChangedAt(DateTime changedAt) =>
        changedAt == default ? DateTime.UtcNow : changedAt;
}
