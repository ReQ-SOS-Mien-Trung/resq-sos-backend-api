using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandHandler(
    IMissionActivityStatusExecutionService missionActivityStatusExecutionService,
    IOperationalHubService operationalHubService,
    IAdminRealtimeHubService adminRealtimeHubService,
    IUnitOfWork unitOfWork,
    ILogger<UpdateActivityStatusCommandHandler> logger
) : IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>
{
    private readonly IMissionActivityStatusExecutionService _missionActivityStatusExecutionService = missionActivityStatusExecutionService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateActivityStatusCommandHandler> _logger = logger;

    public async Task<UpdateActivityStatusResponse> Handle(UpdateActivityStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating status for MissionId={MissionId} ActivityId={ActivityId} -> {Status}",
            request.MissionId,
            request.ActivityId,
            request.Status);

        MissionActivityStatusExecutionResult? executionResult = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            executionResult = await _missionActivityStatusExecutionService.ApplyAsync(
                request.MissionId,
                request.ActivityId,
                request.Status,
                request.DecisionBy,
                request.ImageUrl,
                cancellationToken);
        });

        if (executionResult?.DepotId is int depotId
            && IsDepotActivityRealtimeCandidate(executionResult.ActivityType))
        {
            await operationalHubService.PushDepotActivityUpdateAsync(
                new DepotActivityRealtimeUpdate
                {
                    ActivityId = executionResult.ActivityId,
                    DepotId = depotId,
                    MissionId = executionResult.MissionId,
                    MissionTeamId = executionResult.MissionTeamId,
                    RescueTeamId = executionResult.RescueTeamId,
                    ActivityType = executionResult.ActivityType ?? string.Empty,
                    Action = "StatusChanged",
                    Status = executionResult.EffectiveStatus.ToString(),
                    EstimatedTime = executionResult.EstimatedTime
                },
                cancellationToken);
        }

        if (executionResult?.DepotId is int inventoryDepotId && executionResult.InventoryChanged)
        {
            await operationalHubService.PushDepotInventoryUpdateAsync(
                inventoryDepotId,
                "MissionActivityStatus",
                cancellationToken);
        }

        if (executionResult is not null)
        {
            await adminRealtimeHubService.PushMissionActivityUpdateAsync(
                new AdminMissionActivityRealtimeUpdate
                {
                    EntityId = executionResult.ActivityId,
                    EntityType = "MissionActivity",
                    ActivityId = executionResult.ActivityId,
                    MissionId = executionResult.MissionId,
                    DepotId = executionResult.DepotId,
                    Action = "StatusChanged",
                    Status = executionResult.EffectiveStatus.ToString(),
                    ChangedAt = DateTime.UtcNow
                },
                cancellationToken);
        }

        return new UpdateActivityStatusResponse
        {
            ActivityId = request.ActivityId,
            Status = executionResult!.EffectiveStatus.ToString(),
            DecisionBy = request.DecisionBy,
            ImageUrl = executionResult.ImageUrl,
            ConsumedItems = executionResult.ConsumedItems
        };
    }

    private static bool IsDepotActivityRealtimeCandidate(string? activityType) =>
        string.Equals(activityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase);
}
