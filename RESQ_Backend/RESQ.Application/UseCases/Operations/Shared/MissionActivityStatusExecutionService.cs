using System.Text.Json;
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
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Operations.Shared;

public class MissionActivityStatusExecutionService(
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IPersonnelQueryRepository personnelQueryRepository,
    IDepotInventoryRepository depotInventoryRepository,
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ITeamIncidentRepository teamIncidentRepository,
    IRescueTeamRepository rescueTeamRepository,
    IUnitOfWork unitOfWork,
    ILogger<MissionActivityStatusExecutionService> logger,
    IAssemblyEventRepository assemblyEventRepository
) : IMissionActivityStatusExecutionService
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IPersonnelQueryRepository _personnelQueryRepository = personnelQueryRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly ITeamIncidentRepository _teamIncidentRepository = teamIncidentRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<MissionActivityStatusExecutionService> _logger = logger;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<MissionActivityStatusExecutionResult> ApplyAsync(
        int expectedMissionId,
        int activityId,
        MissionActivityStatus requestedStatus,
        Guid decisionBy,
        string? imageUrl = null,
        CancellationToken cancellationToken = default)
    {
        var activity = await _activityRepository.GetByIdAsync(activityId, cancellationToken);
        if (activity is null)
            throw MissionActivitySyncErrorCodes.WithCode(
                new NotFoundException($"Không tìm th?y activity v?i ID: {activityId}"),
                MissionActivitySyncErrorCodes.ActivityNotFound);

        if (activity.MissionId != expectedMissionId)
            throw MissionActivitySyncErrorCodes.WithCode(
                new BadRequestException("Activity này không thu?c mission du?c ch? d?nh."),
                MissionActivitySyncErrorCodes.MissionActivityMismatch);

        if (activity.MissionTeamId.HasValue)
        {
            var userTeam = await _personnelQueryRepository.GetActiveRescueTeamByUserIdAsync(decisionBy, cancellationToken);
            if (userTeam is not null)
            {
                var missionTeam = await _missionTeamRepository.GetByIdAsync(activity.MissionTeamId.Value, cancellationToken);
                if (missionTeam is not null && missionTeam.RescuerTeamId != userTeam.Id)
                    throw MissionActivitySyncErrorCodes.WithCode(
                        new ForbiddenException("B?n không có quy?n c?p nh?t tr?ng thái activity này. Activity du?c giao cho d?i khác."),
                        MissionActivitySyncErrorCodes.ForbiddenTeamMismatch);
            }
        }

        MissionTeamModel? assignedMissionTeam = null;
        if (activity.MissionTeamId.HasValue)
        {
            assignedMissionTeam = await _missionTeamRepository.GetByIdAsync(activity.MissionTeamId.Value, cancellationToken);
        }

        var effectiveStatus = requestedStatus;
        if (string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
            && requestedStatus == MissionActivityStatus.Succeed
            && activity.Status == MissionActivityStatus.OnGoing)
        {
            effectiveStatus = MissionActivityStatus.PendingConfirmation;
            _logger.LogInformation(
                "RETURN_SUPPLIES ActivityId={ActivityId}: intercepted Succeed -> PendingConfirmation (awaiting depot manager confirmation)",
                activityId);
        }

        if (effectiveStatus == MissionActivityStatus.OnGoing
            && activity.MissionId.HasValue
            && activity.MissionTeamId.HasValue)
        {
            var missionActivities = (await _activityRepository
                .GetByMissionIdAsync(activity.MissionId.Value, cancellationToken))
                .ToList();

            if (MissionActivitySequenceHelper.HasActiveActivityForTeam(
                missionActivities,
                activity.MissionTeamId.Value,
                activity.Id))
            {
                throw MissionActivitySyncErrorCodes.WithCode(
                    new BadRequestException(
                        "Ð?i c?u h? dang có activity khác ? tr?ng thái dang th?c hi?n ho?c ch? xác nh?n. " +
                        "Vui lòng hoàn thành activity hi?n t?i tru?c khi b?t d?u activity ti?p theo."),
                    MissionActivitySyncErrorCodes.ActivitySequenceBlocked);
            }

            var earliestUnfinishedActivity = MissionActivitySequenceHelper
                .GetEarliestUnfinishedActivityForSameTeam(missionActivities, activity);

            if (earliestUnfinishedActivity is not null && earliestUnfinishedActivity.Id != activity.Id)
            {
                throw MissionActivitySyncErrorCodes.WithCode(
                    new BadRequestException(
                        $"Không th? b?t d?u activity #{activity.Id}. " +
                        $"Ð?i c?u h? ph?i hoàn thành activity #{earliestUnfinishedActivity.Id} tru?c khi thao tác activity ti?p theo."),
                    MissionActivitySyncErrorCodes.ActivitySequenceBlocked);
            }
        }

        try
        {
            MissionActivityStateMachine.EnsureValidTransition(activity.Status, effectiveStatus);
        }
        catch (BadRequestException ex)
        {
            throw MissionActivitySyncErrorCodes.WithCode(ex, MissionActivitySyncErrorCodes.InvalidStatusTransition);
        }

        var persistedImageUrl = ShouldPersistEvidenceImage(requestedStatus) && !string.IsNullOrWhiteSpace(imageUrl)
            ? imageUrl.Trim()
            : null;

        await _activityRepository.UpdateStatusAsync(activityId, effectiveStatus, decisionBy, persistedImageUrl, cancellationToken);
        activity.Status = effectiveStatus;
        if (persistedImageUrl is not null)
        {
            activity.ImageUrl = persistedImageUrl;
        }

        if (assignedMissionTeam is not null
            && string.Equals(assignedMissionTeam.Status, MissionTeamExecutionStatus.Assigned.ToString(), StringComparison.OrdinalIgnoreCase)
            && effectiveStatus is MissionActivityStatus.OnGoing or MissionActivityStatus.Succeed)
        {
            await _missionTeamRepository.UpdateStatusAsync(
                assignedMissionTeam.Id,
                MissionTeamExecutionStatus.InProgress.ToString(),
                cancellationToken);
        }

        var isCollectActivity = string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase);
        var pickupExecution = new MissionSupplyPickupExecutionResult();

        if (effectiveStatus == MissionActivityStatus.Succeed
            && isCollectActivity
            && activity.DepotId.HasValue
            && !string.IsNullOrWhiteSpace(activity.Items))
        {
            var items = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts);
            if (items is { Count: > 0 })
            {
                var itemsToConsume = items
                    .Where(item => item.ItemId.HasValue && item.Quantity > 0)
                    .Select(item => ((int ItemModelId, int Quantity))(item.ItemId!.Value, item.Quantity + (item.BufferUsedQuantity ?? 0)))
                    .ToList();

                if (itemsToConsume.Count > 0)
                {
                    pickupExecution = await _depotInventoryRepository.ConsumeReservedSuppliesAsync(
                        activity.DepotId.Value,
                        itemsToConsume,
                        decisionBy,
                        activityId,
                        activity.MissionId ?? 0,
                        cancellationToken);

                    var unusedBufferItems = items
                        .Where(item => item.ItemId.HasValue && (item.BufferQuantity ?? 0) > (item.BufferUsedQuantity ?? 0))
                        .Select(item => ((int ItemModelId, int Quantity))(item.ItemId!.Value, (item.BufferQuantity ?? 0) - (item.BufferUsedQuantity ?? 0)))
                        .ToList();
                    if (unusedBufferItems.Count > 0)
                    {
                        try
                        {
                            await _depotInventoryRepository.ReleaseReservedSuppliesAsync(activity.DepotId.Value, unusedBufferItems, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Failed to release unused buffer for ActivityId={ActivityId}. Reserved stock may be slightly over-locked.",
                                activity.Id);
                        }
                    }

                    await MissionSupplyExecutionSnapshotHelper.SyncPickupExecutionAsync(
                        activity,
                        pickupExecution,
                        _activityRepository,
                        _logger,
                        cancellationToken);

                    _logger.LogInformation(
                        "Inventory consumed for ActivityId={ActivityId} DepotId={DepotId}: {Count} item type(s), bufferUsed={BufferUsed}",
                        activity.Id,
                        activity.DepotId.Value,
                        itemsToConsume.Count,
                        items.Sum(item => item.BufferUsedQuantity ?? 0));
                }
            }
        }

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
                        .Where(item => item.ItemId.HasValue && item.Quantity > 0)
                        .Select(item => ((int ItemModelId, int Quantity))(item.ItemId!.Value, item.Quantity + (item.BufferQuantity ?? 0)))
                        .ToList();

                    if (itemsToRelease.Count > 0)
                    {
                        await _depotInventoryRepository.ReleaseReservedSuppliesAsync(activity.DepotId.Value, itemsToRelease, cancellationToken);
                        _logger.LogInformation(
                            "Reservation released for ActivityId={ActivityId} DepotId={DepotId} due to status={Status}: {Count} item type(s) (incl. buffer)",
                            activity.Id,
                            activity.DepotId.Value,
                            effectiveStatus,
                            itemsToRelease.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "INVENTORY ALERT: Failed to release reservation for ActivityId={ActivityId} DepotId={DepotId}. Reserved stock may be incorrectly locked. Manual reconciliation required.",
                    activity.Id,
                    activity.DepotId.Value);
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
                "Updated MissionTeamId={MissionTeamId} location from ActivityId={ActivityId} ({LocationSource}) to {Latitude},{Longitude}",
                assignedMissionTeam.Id,
                activity.Id,
                locationSource,
                latitude,
                longitude);
        }

        if (activity.MissionId.HasValue && activity.SosRequestId.HasValue)
        {
            var missionActivities = (await _activityRepository.GetByMissionIdAsync(activity.MissionId.Value, cancellationToken)).ToList();
            var updatedActivity = missionActivities.FirstOrDefault(item => item.Id == activity.Id);

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

        var isDeliverActivity = string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase);
        if (effectiveStatus == MissionActivityStatus.Failed
            && isDeliverActivity
            && !string.IsNullOrWhiteSpace(activity.Items)
            && activity.DepotId.HasValue)
        {
            await CreateReturnSuppliesActivityAsync(activity, decisionBy, cancellationToken);
        }

        if (effectiveStatus is MissionActivityStatus.Succeed or MissionActivityStatus.Failed or MissionActivityStatus.Cancelled)
        {
            var autoStartedNextActivityId = await MissionActivityAutoStartHelper.AutoStartNextActivityForSameTeamAsync(
                activity,
                decisionBy,
                _activityRepository,
                _missionTeamRepository,
                _logger,
                cancellationToken);

            if (autoStartedNextActivityId.HasValue)
            {
                await _unitOfWork.SaveAsync();
            }
        }

        if (effectiveStatus == MissionActivityStatus.Succeed
            && string.Equals(activity.ActivityType, MissionReturnAssemblyPointStepHelper.ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase)
            && assignedMissionTeam is not null
            && activity.AssemblyPointId.HasValue)
        {
            await AutoReturnCheckInMissionTeamAsync(activity, assignedMissionTeam, cancellationToken);
        }

        return new MissionActivityStatusExecutionResult
        {
            EffectiveStatus = effectiveStatus,
            CurrentServerStatus = activity.Status,
            ImageUrl = activity.ImageUrl,
            ConsumedItems = pickupExecution.Items
        };
    }

    private static bool ShouldPersistEvidenceImage(MissionActivityStatus requestedStatus) =>
        requestedStatus is MissionActivityStatus.Succeed or MissionActivityStatus.Failed;

    /// <summary>
    /// T? d?ng check-in l?i t?t c? thành viên c?a team vào s? ki?n t?p k?t khi RETURN_ASSEMBLY_POINT hoàn thành.
    /// </summary>
    private async Task AutoReturnCheckInMissionTeamAsync(
        MissionActivityModel activity,
        MissionTeamModel team,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!activity.AssemblyPointId.HasValue)
            {
                _logger.LogInformation(
                    "AutoReturnCheckIn: RETURN_ASSEMBLY_POINT ActivityId={ActivityId} has no AssemblyPointId. Skipping.",
                    activity.Id);
                return;
            }

            var assemblyPointId = activity.AssemblyPointId.Value;
            var shouldSave = await MoveRescueTeamToAssemblyPointAsync(team, assemblyPointId, cancellationToken);
            var activeEvent = await _assemblyEventRepository.GetActiveEventByAssemblyPointAsync(
                assemblyPointId, cancellationToken);

            if (activeEvent is null)
            {
                _logger.LogInformation(
                    "AutoReturnCheckIn: no active assembly event at AssemblyPointId={AssemblyPointId} for MissionTeamId={TeamId}. Skipping.",
                    assemblyPointId, team.Id);
                if (shouldSave)
                {
                    await _unitOfWork.SaveAsync();
                }
                return;
            }

            var eventId = activeEvent.Value.EventId;

            var acceptedMemberIds = team.RescueTeamMembers
                .Where(m => string.Equals(m.Status, TeamMemberStatus.Accepted.ToString(), StringComparison.OrdinalIgnoreCase))
                .Select(m => m.UserId)
                .ToList();

            if (acceptedMemberIds.Count > 0)
            {
                foreach (var userId in acceptedMemberIds)
                {
                    try
                    {
                        var checkedIn = await _assemblyEventRepository.ReturnCheckInAsync(eventId, userId, cancellationToken);
                        shouldSave |= checkedIn;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "AutoReturnCheckIn failed for UserId={UserId} EventId={EventId} TeamId={TeamId}",
                            userId, eventId, team.Id);
                    }
                }

                _logger.LogInformation(
                    "AutoReturnCheckIn MissionTeamId={TeamId} EventId={EventId}: {Count} member(s) checked in at AssemblyPointId={AssemblyPointId}",
                    team.Id, eventId, acceptedMemberIds.Count, assemblyPointId);
            }

            if (shouldSave)
            {
                await _unitOfWork.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AutoReturnCheckIn failed for MissionTeamId={TeamId}. Activity flow continues.", team.Id);
        }
    }

    private async Task<bool> MoveRescueTeamToAssemblyPointAsync(
        MissionTeamModel missionTeam,
        int assemblyPointId,
        CancellationToken cancellationToken)
    {
        if (missionTeam.RescuerTeamId <= 0)
        {
            return false;
        }

        var rescueTeam = await _rescueTeamRepository.GetByIdAsync(missionTeam.RescuerTeamId, cancellationToken);
        if (rescueTeam is null)
        {
            _logger.LogInformation(
                "AutoReturnCheckIn: rescue team not found for MissionTeamId={MissionTeamId}, RescuerTeamId={RescuerTeamId}.",
                missionTeam.Id,
                missionTeam.RescuerTeamId);
            return false;
        }

        if (rescueTeam.AssemblyPointId == assemblyPointId)
        {
            return false;
        }

        rescueTeam.AssignToAssemblyPoint(assemblyPointId);
        await _rescueTeamRepository.UpdateAsync(rescueTeam, cancellationToken);
        return true;
    }

    private static bool TryGetActivityLocation(MissionActivityModel activity, out double latitude, out double longitude, out string locationSource)
    {
        if (string.Equals(activity.ActivityType, MissionReturnAssemblyPointStepHelper.ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase)
            && activity.AssemblyPointLatitude.HasValue
            && activity.AssemblyPointLongitude.HasValue)
        {
            latitude = activity.AssemblyPointLatitude.Value;
            longitude = activity.AssemblyPointLongitude.Value;
            locationSource = "MissionActivity.AssemblyPoint";
            return true;
        }

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
            var existingActivities = (await _activityRepository.GetByMissionIdAsync(missionId, cancellationToken)).ToList();
            var insertionStep = MissionReturnAssemblyPointStepHelper.ReserveStepBeforeReturnAssemblyPoint(
                existingActivities,
                failedActivity.MissionTeamId,
                out var shiftedActivities);

            foreach (var shiftedActivity in shiftedActivities)
                await _activityRepository.UpdateAsync(shiftedActivity, cancellationToken);

            var returnActivity = new MissionActivityModel
            {
                MissionId = missionId,
                Step = insertionStep,
                ActivityType = "RETURN_SUPPLIES",
                Description = $"Tr? v?t ph?m v? kho {failedActivity.DepotName} do giao hàng th?t b?i (Activity #{failedActivity.Id})",
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
                "Auto-created RETURN_SUPPLIES ActivityId={ReturnActivityId} for failed DELIVER_SUPPLIES ActivityId={FailedActivityId} MissionId={MissionId}",
                returnActivityId,
                failedActivity.Id,
                missionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to auto-create RETURN_SUPPLIES activity for failed DELIVER_SUPPLIES ActivityId={ActivityId} MissionId={MissionId}",
                failedActivity.Id,
                failedActivity.MissionId);
        }
    }
}
