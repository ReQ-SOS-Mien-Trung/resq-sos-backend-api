using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandHandler(
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IPersonnelQueryRepository personnelQueryRepository,
    IDepotInventoryRepository depotInventoryRepository,
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ITeamIncidentRepository teamIncidentRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateActivityStatusCommandHandler> logger
) : IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IPersonnelQueryRepository _personnelQueryRepository = personnelQueryRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly ITeamIncidentRepository _teamIncidentRepository = teamIncidentRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateActivityStatusCommandHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<UpdateActivityStatusResponse> Handle(UpdateActivityStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating status for ActivityId={activityId} -> {status}", request.ActivityId, request.Status);

        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken);
        if (activity is null)
            throw new NotFoundException($"Không tìm thấy activity với ID: {request.ActivityId}");

        // If the activity is assigned to a specific team, enforce that the requester belongs to that team
        if (activity.MissionTeamId.HasValue)
        {
            var userTeam = await _personnelQueryRepository.GetActiveRescueTeamByUserIdAsync(request.DecisionBy, cancellationToken);
            if (userTeam is not null)
            {
                var missionTeam = await _missionTeamRepository.GetByIdAsync(activity.MissionTeamId.Value, cancellationToken);
                if (missionTeam is not null && missionTeam.RescuerTeamId != userTeam.Id)
                    throw new ForbiddenException("Bạn không có quyền cập nhật trạng thái activity này. Activity được giao cho đội khác.");
            }
        }

        MissionTeamModel? assignedMissionTeam = null;
        if (activity.MissionTeamId.HasValue)
        {
            assignedMissionTeam = await _missionTeamRepository.GetByIdAsync(activity.MissionTeamId.Value, cancellationToken);
        }

        // Intercept: RETURN_SUPPLIES cannot go directly to Succeed — must go through PendingConfirmation first
        var effectiveStatus = request.Status;
        if (string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
            && request.Status == MissionActivityStatus.Succeed
            && activity.Status == MissionActivityStatus.OnGoing)
        {
            effectiveStatus = MissionActivityStatus.PendingConfirmation;
            _logger.LogInformation(
                "RETURN_SUPPLIES ActivityId={activityId}: intercepted Succeed → PendingConfirmation (awaiting depot manager confirmation)",
                request.ActivityId);
        }

        MissionActivityStateMachine.EnsureValidTransition(activity.Status, effectiveStatus);

        await _activityRepository.UpdateStatusAsync(request.ActivityId, effectiveStatus, request.DecisionBy, cancellationToken);
        activity.Status = effectiveStatus;

        if (assignedMissionTeam is not null
            && string.Equals(assignedMissionTeam.Status, MissionTeamExecutionStatus.Assigned.ToString(), StringComparison.OrdinalIgnoreCase)
            && effectiveStatus is MissionActivityStatus.OnGoing or MissionActivityStatus.Succeed)
        {
            await _missionTeamRepository.UpdateStatusAsync(
                assignedMissionTeam.Id,
                MissionTeamExecutionStatus.InProgress.ToString(),
                cancellationToken);
        }

        // Only COLLECT_SUPPLIES activities affect depot inventory.
        // DELIVER_SUPPLIES, RESCUE, MEDICAL_AID, EVACUATE do not consume stock directly.
        var isCollectActivity = string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase);
        var pickupExecution = new MissionSupplyPickupExecutionResult();

        // Side-effect: consume inventory when a COLLECT_SUPPLIES activity succeeds
        // (team physically arrived at depot and picked up the supplies)
        if (effectiveStatus == MissionActivityStatus.Succeed
            && isCollectActivity
            && activity.DepotId.HasValue
            && !string.IsNullOrWhiteSpace(activity.Items))
        {
            var items = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts);
            if (items is { Count: > 0 })
            {
                var itemsToConsume = items
                    .Where(i => i.ItemId.HasValue && i.Quantity > 0)
                    .Select(i => (ItemModelId: i.ItemId!.Value, Quantity: i.Quantity))
                    .ToList();

                if (itemsToConsume.Count > 0)
                {
                    // Do NOT catch here — if inventory deduction fails the whole transaction
                    // should roll back so the activity is NOT marked Succeed with stale stock.
                    pickupExecution = await _depotInventoryRepository.ConsumeReservedSuppliesAsync(
                        activity.DepotId.Value, itemsToConsume, request.DecisionBy,
                        request.ActivityId, activity.MissionId ?? 0, cancellationToken);

                    await MissionSupplyExecutionSnapshotHelper.SyncPickupExecutionAsync(
                        activity,
                        pickupExecution,
                        _activityRepository,
                        _logger,
                        cancellationToken);

                    _logger.LogInformation(
                        "Inventory consumed for ActivityId={activityId} DepotId={depotId}: {count} item type(s)",
                        activity.Id, activity.DepotId.Value, itemsToConsume.Count);
                }
            }
        }

        // Side-effect: release reservations when a COLLECT_SUPPLIES activity is cancelled OR failed.
        // Both outcomes mean the team will NOT pick up supplies → reserved stock must be freed.
        var shouldRelease = effectiveStatus is MissionActivityStatus.Cancelled or MissionActivityStatus.Failed;
        if (shouldRelease
            && isCollectActivity
            && activity.DepotId.HasValue
            && !string.IsNullOrWhiteSpace(activity.Items))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts);
                if (items is { Count: > 0 })
                {
                    var itemsToRelease = items
                        .Where(i => i.ItemId.HasValue && i.Quantity > 0)
                        .Select(i => (ItemModelId: i.ItemId!.Value, Quantity: i.Quantity))
                        .ToList();

                    if (itemsToRelease.Count > 0)
                    {
                        await _depotInventoryRepository.ReleaseReservedSuppliesAsync(activity.DepotId.Value, itemsToRelease, cancellationToken);
                        _logger.LogInformation(
                            "Reservation released for ActivityId={activityId} DepotId={depotId} due to status={status}: {count} item type(s)",
                            activity.Id, activity.DepotId.Value, effectiveStatus, itemsToRelease.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                // Release failure is non-critical (stock stays over-reserved temporarily),
                // but we must log it prominently so ops can manually reconcile.
                _logger.LogError(ex,
                    "INVENTORY ALERT: Failed to release reservation for ActivityId={activityId} DepotId={depotId}. " +
                    "Reserved stock may be incorrectly locked. Manual reconciliation required.",
                    activity.Id, activity.DepotId.Value);
            }
        }

        if (assignedMissionTeam is not null
            && effectiveStatus is MissionActivityStatus.Succeed or MissionActivityStatus.PendingConfirmation
            && TryGetActivityLocation(activity, out var latitude, out var longitude, out var locationSource))
        {
            await _missionTeamRepository.UpdateCurrentLocationAsync(
                assignedMissionTeam.Id,
                latitude,
                longitude,
                $"{locationSource}:{activity.Id}",
                cancellationToken);

            _logger.LogInformation(
                "Updated MissionTeamId={missionTeamId} location from ActivityId={activityId} ({locationSource}) to {latitude},{longitude}",
                assignedMissionTeam.Id, activity.Id, locationSource, latitude, longitude);
        }

        if (activity.MissionId.HasValue && activity.SosRequestId.HasValue)
        {
            var missionActivities = (await _activityRepository.GetByMissionIdAsync(activity.MissionId.Value, cancellationToken)).ToList();
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
                _sosRequestRepository,
                _sosRequestUpdateRepository,
                _activityRepository,
                _teamIncidentRepository,
                _logger,
                cancellationToken);
        }

        await _unitOfWork.SaveAsync();

        // Side-effect: auto-create RETURN_SUPPLIES when a DELIVER_SUPPLIES activity fails
        var isDeliverActivity = string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase);
        if (effectiveStatus == MissionActivityStatus.Failed
            && isDeliverActivity
            && !string.IsNullOrWhiteSpace(activity.Items)
            && activity.DepotId.HasValue)
        {
            await CreateReturnSuppliesActivityAsync(activity, request.DecisionBy, cancellationToken);
        }

        if (effectiveStatus is MissionActivityStatus.Succeed or MissionActivityStatus.Failed or MissionActivityStatus.Cancelled)
        {
            var autoStartedNextActivityId = await MissionActivityAutoStartHelper.AutoStartNextActivityForSameTeamAsync(
                activity,
                request.DecisionBy,
                _activityRepository,
                _missionTeamRepository,
                _logger,
                cancellationToken);

            if (autoStartedNextActivityId.HasValue)
            {
                await _unitOfWork.SaveAsync();
            }
        }

        return new UpdateActivityStatusResponse
        {
            ActivityId = request.ActivityId,
            Status = effectiveStatus.ToString(),
            DecisionBy = request.DecisionBy,
            ConsumedItems = pickupExecution.Items
        };
    }

    private static bool TryGetActivityLocation(MissionActivityModel activity, out double latitude, out double longitude, out string locationSource)
    {
        if (activity.TargetLatitude.HasValue && activity.TargetLongitude.HasValue)
        {
            latitude = activity.TargetLatitude.Value;
            longitude = activity.TargetLongitude.Value;
            locationSource = "MissionActivity.Target";
            return true;
        }

        if (activity.AssemblyPointLatitude.HasValue && activity.AssemblyPointLongitude.HasValue)
        {
            latitude = activity.AssemblyPointLatitude.Value;
            longitude = activity.AssemblyPointLongitude.Value;
            locationSource = "MissionActivity.AssemblyPoint";
            return true;
        }

        latitude = default;
        longitude = default;
        locationSource = string.Empty;
        return false;
    }

    private async Task CreateReturnSuppliesActivityAsync(MissionActivityModel failedActivity, Guid decisionBy, CancellationToken cancellationToken)
    {
        try
        {
            var missionId = failedActivity.MissionId ?? 0;
            var existingActivities = await _activityRepository.GetByMissionIdAsync(missionId, cancellationToken);
            var maxStep = existingActivities.Any() ? existingActivities.Max(a => a.Step ?? 0) : 0;

            var returnActivity = new MissionActivityModel
            {
                MissionId = missionId,
                Step = maxStep + 1,
                ActivityType = "RETURN_SUPPLIES",
                Description = $"Trả vật tư về kho {failedActivity.DepotName} do giao hàng thất bại (Activity #{failedActivity.Id})",
                Priority = failedActivity.Priority,
                EstimatedTime = failedActivity.EstimatedTime,
                SosRequestId = failedActivity.SosRequestId,
                DepotId = failedActivity.DepotId,
                DepotName = failedActivity.DepotName,
                DepotAddress = failedActivity.DepotAddress,
                Items = failedActivity.Items,
                Status = MissionActivityStatus.Planned
            };

            var returnActivityId = await _activityRepository.AddAsync(returnActivity, cancellationToken);

            if (failedActivity.MissionTeamId.HasValue)
            {
                await _activityRepository.AssignTeamAsync(returnActivityId, failedActivity.MissionTeamId.Value, cancellationToken);
            }

            await _unitOfWork.SaveAsync();

            _logger.LogInformation(
                "Auto-created RETURN_SUPPLIES ActivityId={returnActivityId} for failed DELIVER_SUPPLIES ActivityId={failedActivityId} MissionId={missionId}",
                returnActivityId, failedActivity.Id, missionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to auto-create RETURN_SUPPLIES activity for failed DELIVER_SUPPLIES ActivityId={activityId} MissionId={missionId}",
                failedActivity.Id, failedActivity.MissionId);
        }
    }
}
