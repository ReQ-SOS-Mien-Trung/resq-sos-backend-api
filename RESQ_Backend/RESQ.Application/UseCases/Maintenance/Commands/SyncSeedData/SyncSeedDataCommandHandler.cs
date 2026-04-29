using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Maintenance.Commands.SyncSeedData;

public sealed class SyncSeedDataCommandHandler(
    ISeedDataSyncService seedDataSyncService,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    IOperationalHubService operationalHubService)
    : IRequestHandler<SyncSeedDataCommand, SeedDataSyncReport>
{
    private readonly ISeedDataSyncService _seedDataSyncService = seedDataSyncService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;

    public async Task<SeedDataSyncReport> Handle(
        SyncSeedDataCommand request,
        CancellationToken cancellationToken)
    {
        if (request.DryRun)
        {
            return await _seedDataSyncService.SyncAsync(dryRun: true, cancellationToken);
        }

        SeedDataSyncReport? report = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            report = await _seedDataSyncService.SyncAsync(dryRun: false, cancellationToken);
            await _unitOfWork.SaveAsync();
        });

        report ??= new SeedDataSyncReport { DryRun = false };
        await PushRealtimeInvalidationsAsync(report, cancellationToken);
        return report;
    }

    private async Task PushRealtimeInvalidationsAsync(
        SeedDataSyncReport report,
        CancellationToken cancellationToken)
    {
        foreach (var campaignId in report.AffectedCampaignIds.Distinct())
        {
            await _adminRealtimeHubService.PushCampaignUpdateAsync(
                new AdminCampaignRealtimeUpdate
                {
                    EntityId = campaignId,
                    EntityType = "Campaign",
                    CampaignId = campaignId,
                    Action = "SeedDataSynced",
                    ChangedAt = report.GeneratedAt
                },
                cancellationToken);
        }

        foreach (var depotId in report.AffectedDepotIds.Distinct())
        {
            await _adminRealtimeHubService.PushDepotUpdateAsync(
                new AdminDepotRealtimeUpdate
                {
                    EntityId = depotId,
                    EntityType = "Depot",
                    DepotId = depotId,
                    Action = "SeedDataSynced",
                    ChangedAt = report.GeneratedAt
                },
                cancellationToken);

            await _operationalHubService.PushDepotInventoryUpdateAsync(
                depotId,
                "SeedDataSynced",
                cancellationToken);
        }

        foreach (var depotId in report.AffectedDepotFundDepotIds.Distinct())
        {
            await _adminRealtimeHubService.PushDisbursementUpdateAsync(
                new AdminDisbursementRealtimeUpdate
                {
                    EntityId = depotId,
                    EntityType = "DepotFund",
                    DisbursementId = null,
                    CampaignId = null,
                    DepotId = depotId,
                    Amount = 0,
                    Action = "SeedDataSynced",
                    ChangedAt = report.GeneratedAt
                },
                cancellationToken);
        }
    }
}
