using System.Text.Json;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionActivityIncidentFailureHelper
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static bool CanFailFromIncident(MissionActivityStatus status) =>
        status is MissionActivityStatus.Planned or MissionActivityStatus.OnGoing or MissionActivityStatus.PendingConfirmation;

    public static async Task<IReadOnlyCollection<int>> FailActivitiesAsync(
        IEnumerable<MissionActivityModel> activities,
        Guid decisionBy,
        IMissionActivityRepository activityRepository,
        IMissionTeamRepository missionTeamRepository,
        ISosRequestRepository sosRequestRepository,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        ITeamIncidentRepository teamIncidentRepository,
        IDepotInventoryRepository depotInventoryRepository,
        IUnitOfWork unitOfWork,
        ILogger logger,
        bool allowAutoChain,
        bool allowReturnSuppliesCreation,
        bool allowSosLifecycleSync,
        CancellationToken cancellationToken)
    {
        var orderedActivities = activities
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .ToList();

        var autoStartedActivityIds = new List<int>();

        foreach (var activity in orderedActivities)
        {
            var autoStartedActivityId = await FailSingleActivityAsync(
                activity,
                decisionBy,
                activityRepository,
                missionTeamRepository,
                sosRequestRepository,
                sosRequestUpdateRepository,
                teamIncidentRepository,
                depotInventoryRepository,
                unitOfWork,
                logger,
                allowAutoChain,
                allowReturnSuppliesCreation,
                allowSosLifecycleSync,
                cancellationToken);

            if (autoStartedActivityId.HasValue)
            {
                autoStartedActivityIds.Add(autoStartedActivityId.Value);
            }
        }

        return autoStartedActivityIds;
    }

    public static async Task<int?> FailSingleActivityAsync(
        MissionActivityModel activity,
        Guid decisionBy,
        IMissionActivityRepository activityRepository,
        IMissionTeamRepository missionTeamRepository,
        ISosRequestRepository sosRequestRepository,
        ISosRequestUpdateRepository sosRequestUpdateRepository,
        ITeamIncidentRepository teamIncidentRepository,
        IDepotInventoryRepository depotInventoryRepository,
        IUnitOfWork unitOfWork,
        ILogger logger,
        bool allowAutoChain,
        bool allowReturnSuppliesCreation,
        bool allowSosLifecycleSync,
        CancellationToken cancellationToken)
    {
        if (!CanFailFromIncident(activity.Status))
        {
            throw new BadRequestException(
                $"Activity #{activity.Id} đang ở trạng thái '{activity.Status}' nên không thể chuyển sang Failed bằng incident.");
        }

        await activityRepository.UpdateStatusAsync(activity.Id, MissionActivityStatus.Failed, decisionBy, cancellationToken: cancellationToken);
        activity.Status = MissionActivityStatus.Failed;

        await ReleaseReservedCollectSuppliesAsync(activity, depotInventoryRepository, logger, cancellationToken);

        if (allowReturnSuppliesCreation)
        {
            await CreateReturnSuppliesActivityAsync(activity, activityRepository, unitOfWork, logger, cancellationToken);
        }

        if (allowSosLifecycleSync && activity.MissionId.HasValue && activity.SosRequestId.HasValue)
        {
            var missionActivities = (await activityRepository.GetByMissionIdAsync(activity.MissionId.Value, cancellationToken)).ToList();
            var updatedActivity = missionActivities.FirstOrDefault(x => x.Id == activity.Id);

            if (updatedActivity is not null)
            {
                updatedActivity.Status = activity.Status;
            }
            else
            {
                missionActivities.Add(activity);
            }

            await MissionActivitySosRequestSyncHelper.SyncTouchedSosRequestsAsync(
                [activity.SosRequestId],
                missionActivities,
                sosRequestRepository,
                sosRequestUpdateRepository,
                activityRepository,
                teamIncidentRepository,
                logger,
                cancellationToken);
        }

        if (!allowAutoChain)
        {
            return null;
        }

        return await MissionActivityAutoStartHelper.AutoStartNextActivityForSameTeamAsync(
            activity,
            decisionBy,
            activityRepository,
            missionTeamRepository,
            logger,
            cancellationToken);
    }

    private static async Task ReleaseReservedCollectSuppliesAsync(
        MissionActivityModel activity,
        IDepotInventoryRepository depotInventoryRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var isCollectActivity = string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase);
        if (!isCollectActivity || !activity.DepotId.HasValue || string.IsNullOrWhiteSpace(activity.Items))
        {
            return;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts);
            if (items is not { Count: > 0 })
            {
                return;
            }

            var itemsToRelease = items
                .Where(item => item.ItemId.HasValue && item.Quantity > 0)
                .Select(item => (ItemModelId: item.ItemId!.Value, Quantity: item.Quantity + (item.BufferQuantity ?? 0)))
                .ToList();

            if (itemsToRelease.Count == 0)
            {
                return;
            }

            await depotInventoryRepository.ReleaseReservedSuppliesAsync(activity.DepotId.Value, itemsToRelease, cancellationToken);
            logger.LogInformation(
                "Reservation released for incident-failed ActivityId={ActivityId} DepotId={DepotId}: {Count} item type(s)",
                activity.Id,
                activity.DepotId.Value,
                itemsToRelease.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "INVENTORY ALERT: Failed to release reservation for incident-failed ActivityId={ActivityId} DepotId={DepotId}. Manual reconciliation required.",
                activity.Id,
                activity.DepotId);
        }
    }

    private static async Task CreateReturnSuppliesActivityAsync(
        MissionActivityModel failedActivity,
        IMissionActivityRepository activityRepository,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var isDeliverActivity = string.Equals(failedActivity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase);
        if (!isDeliverActivity || string.IsNullOrWhiteSpace(failedActivity.Items) || !failedActivity.DepotId.HasValue)
        {
            return;
        }

        try
        {
            var missionId = failedActivity.MissionId ?? 0;
            var existingActivities = (await activityRepository.GetByMissionIdAsync(missionId, cancellationToken)).ToList();
            var insertionStep = MissionReturnAssemblyPointStepHelper.ReserveStepBeforeReturnAssemblyPoint(
                existingActivities,
                failedActivity.MissionTeamId,
                out var shiftedActivities);

            foreach (var shiftedActivity in shiftedActivities)
                await activityRepository.UpdateAsync(shiftedActivity, cancellationToken);

            var returnActivity = new MissionActivityModel
            {
                MissionId = missionId,
                Step = insertionStep,
                ActivityType = "RETURN_SUPPLIES",
                Description = $"Trả vật phẩm về kho {failedActivity.DepotName} do giao hàng thất bại (Activity #{failedActivity.Id})",
                Priority = failedActivity.Priority,
                EstimatedTime = failedActivity.EstimatedTime,
                SosRequestId = failedActivity.SosRequestId,
                DepotId = failedActivity.DepotId,
                DepotName = failedActivity.DepotName,
                DepotAddress = failedActivity.DepotAddress,
                Items = failedActivity.Items,
                Status = MissionActivityStatus.Planned
            };

            var returnActivityId = await activityRepository.AddAsync(returnActivity, cancellationToken);

            if (failedActivity.MissionTeamId.HasValue)
            {
                await activityRepository.AssignTeamAsync(returnActivityId, failedActivity.MissionTeamId.Value, cancellationToken);
                await unitOfWork.SaveAsync();
            }

            logger.LogInformation(
                "Auto-created RETURN_SUPPLIES ActivityId={ReturnActivityId} for incident-failed DELIVER_SUPPLIES ActivityId={FailedActivityId} MissionId={MissionId}",
                returnActivityId,
                failedActivity.Id,
                missionId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to auto-create RETURN_SUPPLIES activity for incident-failed DELIVER_SUPPLIES ActivityId={ActivityId} MissionId={MissionId}",
                failedActivity.Id,
                failedActivity.MissionId);
        }
    }
}
