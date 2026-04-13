using MediatR;
using RESQ.Application.Extensions;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Emergency;
using RESQ.Domain.Enum.Operations;
using System.Text.Json;

namespace RESQ.Application.UseCases.Operations.Commands.CreateMission;

public class CreateMissionCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository missionActivityRepository,
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IRescueTeamRepository rescueTeamRepository,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    IFirebaseService firebaseService,
    ILogger<CreateMissionCommandHandler> logger
) : IRequestHandler<CreateMissionCommand, CreateMissionResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _missionActivityRepository = missionActivityRepository;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IMediator _mediator = mediator;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<CreateMissionCommandHandler> _logger = logger;

    private const string CollectSuppliesActivityType = "COLLECT_SUPPLIES";
    private const string ReturnSuppliesActivityType = "RETURN_SUPPLIES";
    private const string ReusableItemType = "Reusable";
    private const double DefaultBufferRatio = 0.10;

    public async Task<CreateMissionResponse> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating mission for ClusterId={clusterId}, CreatedBy={userId}",
            request.ClusterId, request.CreatedById);

        // Validate cluster exists
        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        // Validate depot inventory for each activity that specifies supplies
        await ValidateSuppliesAsync(request.Activities, cancellationToken);
        await ValidateReusableReturnActivitiesAsync(request.Activities, cancellationToken);

        // Build domain model
        var mission = new MissionModel
        {
            ClusterId = request.ClusterId,
            MissionType = request.MissionType,
            PriorityScore = request.PriorityScore,
            Status = MissionStatus.Planned,
            StartTime = request.StartTime.ToUtcForStorage(),
            ExpectedEndTime = request.ExpectedEndTime.ToUtcForStorage(),
            IsCompleted = false,
            CreatedById = request.CreatedById,
            CreatedAt = DateTime.UtcNow,
            Activities = request.Activities.Select((a, idx) => new MissionActivityModel
            {
                Step = a.Step ?? (idx + 1),
                ActivityType = a.ActivityType,
                Description = a.Description,
                Priority = a.Priority,
                EstimatedTime = a.EstimatedTime,
                SosRequestId = a.SosRequestId,
                DepotId = a.DepotId,
                DepotName = a.DepotName,
                DepotAddress = a.DepotAddress,
                AssemblyPointId = a.AssemblyPointId,
                Items = a.SuppliesToCollect is { Count: > 0 }
                    ? JsonSerializer.Serialize(a.SuppliesToCollect.Select(s =>
                    {
                        var bufferRatio = Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
                        var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling((s.Quantity ?? 0) * bufferRatio) : 0;
                        return new SupplyToCollectDto
                        {
                            ItemId = s.Id,
                            ItemName = s.Name ?? string.Empty,
                            Quantity = s.Quantity ?? 0,
                            Unit = s.Unit,
                            BufferRatio = bufferRatio > 0 ? bufferRatio : (double?)null,
                            BufferQuantity = bufferQty > 0 ? bufferQty : (int?)null
                        };
                    }))
                    : null,
                Target = a.Target,
                TargetLatitude = a.TargetLatitude,
                TargetLongitude = a.TargetLongitude,
                Status = MissionActivityStatus.Planned
            }).ToList()
        };

        var missionId = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            missionId = await _missionRepository.CreateAsync(mission, request.CreatedById, cancellationToken);

            // Mark cluster as having a mission created
            cluster.IsMissionCreated = true;
            await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);

            // Update all SOS requests in cluster to Assigned
            await _sosRequestRepository.UpdateStatusByClusterIdAsync(request.ClusterId, SosRequestStatus.Assigned, cancellationToken);

            await _unitOfWork.SaveAsync();

            // Assign rescue teams per activity (using suggestedTeam.id / RescueTeamId)
            var savedActivities = await _missionRepository.GetByIdAsync(missionId, cancellationToken);
            if (savedActivities?.Activities is not null)
            {
                var activityList = savedActivities.Activities
                    .OrderBy(a => a.Step)
                    .ToList();

                var requestActivities = request.Activities
                    .OrderBy(a => a.Step ?? int.MaxValue)
                    .ToList();

                for (int i = 0; i < Math.Min(activityList.Count, requestActivities.Count); i++)
                {
                    var act = requestActivities[i];
                    if (!act.RescueTeamId.HasValue) continue;

                    try
                    {
                        var assignCmd = new AssignTeamToActivityCommand(
                            activityList[i].Id,
                            missionId,
                            act.RescueTeamId.Value,
                            request.CreatedById
                        );
                        await _mediator.Send(assignCmd, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Could not assign RescueTeamId={teamId} to ActivityId={actId} in MissionId={missionId}",
                            act.RescueTeamId.Value, activityList[i].Id, missionId);
                    }
                }
            }

            savedActivities = await _missionRepository.GetByIdAsync(missionId, cancellationToken);
            if (savedActivities?.Activities is not null)
            {
                var persistedActivities = savedActivities.Activities
                    .OrderBy(a => a.Step ?? int.MaxValue)
                    .ThenBy(a => a.Id)
                    .ToList();
                var requestActivities = request.Activities
                    .OrderBy(a => a.Step ?? int.MaxValue)
                    .ToList();

                await ReserveSuppliesAsync(requestActivities, persistedActivities, cancellationToken);
            }
        });

        _logger.LogInformation("Mission created: MissionId={missionId}", missionId);

        // Notify team members about the new mission assignment
        await NotifyTeamMembersAsync(request.Activities, missionId, cancellationToken);

        return new CreateMissionResponse
        {
            MissionId = missionId,
            ClusterId = request.ClusterId,
            MissionType = request.MissionType,
            Status = "planned",
            ActivityCount = request.Activities.Count,
            CreatedAt = mission.CreatedAt
        };
    }

    private async Task ValidateSuppliesAsync(List<CreateActivityItemDto> activities, CancellationToken cancellationToken)
    {
        // Only COLLECT_SUPPLIES steps actually draw from depot inventory.
        // DELIVER_SUPPLIES (and others) may carry suppliesToCollect metadata but do NOT
        // represent a separate depot withdrawal - including them would double-count quantities.
        var activitiesWithSupplies = activities
            .Where(a => IsCollectSuppliesActivity(a)
                && a.DepotId.HasValue
                && a.SuppliesToCollect is { Count: > 0 })
            .ToList();

        // Aggregate items per depot (sum quantities if same item appears in multiple activities of same depot)
        var byDepot = activitiesWithSupplies
            .GroupBy(a => a.DepotId!.Value)
            .Select(g => new
            {
                DepotId = g.Key,
                Items = g.SelectMany(a => a.SuppliesToCollect!)
                         .Where(s => s.Id.HasValue)
                         .GroupBy(s => s.Id!.Value)
                         .Select(sg =>
                         {
                             var first = sg.First();
                             var totalRequired = sg.Sum(s => s.Quantity ?? 0);
                             var bufferRatio = Math.Max(0.0, first.BufferRatio ?? DefaultBufferRatio);
                             var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling(totalRequired * bufferRatio) : 0;
                             return (
                                 ItemModelId: sg.Key,
                                 ItemName: first.Name ?? $"Item#{sg.Key}",
                                 RequestedQuantity: totalRequired + bufferQty
                             );
                         })
                         .ToList()
            });

        var allErrors = new List<string>();

        foreach (var depot in byDepot)
        {
            if (depot.Items.Count == 0) continue;

            var shortages = await _depotInventoryRepository.CheckSupplyAvailabilityAsync(
                depot.DepotId, depot.Items, cancellationToken);

            foreach (var s in shortages)
            {
                allErrors.Add(s.NotFound
                    ? $"Kho {depot.DepotId}: Vật tư '{s.ItemName}' (ID={s.ItemModelId}) không có trong kho."
                    : $"Kho {depot.DepotId}: Vật tư '{s.ItemName}' (ID={s.ItemModelId}) không đủ số lượng — yêu cầu {s.RequestedQuantity}, khả dụng {s.AvailableQuantity}.");
            }
        }

        if (allErrors.Count > 0)
            throw new BadRequestException($"Kiểm tra tồn kho thất bại:\n{string.Join("\n", allErrors)}");
    }

    private async Task ValidateReusableReturnActivitiesAsync(
        List<CreateActivityItemDto> activities,
        CancellationToken cancellationToken)
    {
        var orderedActivities = activities
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ToList();
        var returnActivities = activities
            .Where(IsReturnSuppliesActivity)
            .ToList();

        var seenReturnActivity = false;
        var orderingErrors = new List<string>();
        foreach (var activity in orderedActivities)
        {
            if (IsReturnSuppliesActivity(activity))
            {
                seenReturnActivity = true;
                continue;
            }

            if (seenReturnActivity)
            {
                orderingErrors.Add(
                    $"RETURN_SUPPLIES phải nằm ở cuối kế hoạch, nhưng phát hiện activity '{activity.ActivityType}' sau bước trả kho (step {activity.Step ?? 0}).");
            }
        }

        var allSupplies = activities
            .SelectMany(activity => activity.SuppliesToCollect ?? [])
            .Where(supply => supply.Id.HasValue)
            .Select(supply => supply.Id!.Value)
            .Distinct()
            .ToList();

        if (allSupplies.Count == 0)
        {
            foreach (var returnActivity in returnActivities)
            {
                orderingErrors.Add(
                    $"RETURN_SUPPLIES step {returnActivity.Step ?? 0} phải có item_id hợp lệ cho vật tư reusable cần trả.");
            }

            if (orderingErrors.Count > 0)
                throw new BadRequestException($"Kế hoạch mission chưa hợp lệ:\n{string.Join("\n", orderingErrors)}");

            return;
        }

        var itemLookup = await _itemModelMetadataRepository.GetByIdsAsync(allSupplies, cancellationToken);
        var missingItemIds = allSupplies
            .Where(itemId => !itemLookup.ContainsKey(itemId))
            .ToList();

        if (missingItemIds.Count > 0)
        {
            throw new BadRequestException(
                $"Không tìm thấy metadata cho các item_id: {string.Join(", ", missingItemIds)}.");
        }

        var errors = new List<string>(orderingErrors);
        var requiredReturnItems = new Dictionary<(int DepotId, int TeamId), Dictionary<int, int>>();
        var actualReturnItems = new Dictionary<(int DepotId, int TeamId), Dictionary<int, int>>();

        foreach (var activity in activities)
        {
            if (!IsCollectSuppliesActivity(activity)
                || !activity.DepotId.HasValue
                || !activity.RescueTeamId.HasValue
                || activity.SuppliesToCollect is not { Count: > 0 })
            {
                continue;
            }

            var key = (activity.DepotId.Value, activity.RescueTeamId.Value);
            foreach (var supply in activity.SuppliesToCollect)
            {
                if (!supply.Id.HasValue || (supply.Quantity ?? 0) <= 0)
                    continue;

                if (!IsReusableItem(supply.Id.Value, itemLookup))
                    continue;

                var expectedItems = GetOrCreateItemBucket(requiredReturnItems, key);
                expectedItems[supply.Id.Value] = expectedItems.GetValueOrDefault(supply.Id.Value) + supply.Quantity!.Value;
            }
        }

        foreach (var activity in returnActivities)
        {
            var stepLabel = activity.Step?.ToString() ?? "?";
            if (!activity.DepotId.HasValue)
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} thiếu DepotId.");
                continue;
            }

            if (!activity.RescueTeamId.HasValue)
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} thiếu RescueTeamId.");
                continue;
            }

            if (activity.SuppliesToCollect is not { Count: > 0 })
            {
                errors.Add($"RETURN_SUPPLIES step {stepLabel} phải có danh sách vật tư reusable cần trả.");
                continue;
            }

            var key = (activity.DepotId.Value, activity.RescueTeamId.Value);
            var actualItems = GetOrCreateItemBucket(actualReturnItems, key);
            foreach (var supply in activity.SuppliesToCollect)
            {
                if (!supply.Id.HasValue || (supply.Quantity ?? 0) <= 0)
                {
                    errors.Add($"RETURN_SUPPLIES step {stepLabel} có vật tư thiếu item_id hoặc quantity không hợp lệ.");
                    continue;
                }

                if (!IsReusableItem(supply.Id.Value, itemLookup))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES step {stepLabel} chỉ được chứa vật tư reusable, nhưng item '{ResolveItemName(supply.Id.Value, itemLookup, supply.Name)}' không phải reusable.");
                    continue;
                }

                actualItems[supply.Id.Value] = actualItems.GetValueOrDefault(supply.Id.Value) + supply.Quantity!.Value;
            }
        }

        foreach (var actualGroup in actualReturnItems)
        {
            if (!requiredReturnItems.TryGetValue(actualGroup.Key, out var expectedItems))
            {
                errors.Add(
                    $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, đội {actualGroup.Key.TeamId} không tương ứng với bất kỳ COLLECT_SUPPLIES nào có vật tư reusable.");
                continue;
            }

            foreach (var actualItem in actualGroup.Value)
            {
                if (!expectedItems.TryGetValue(actualItem.Key, out var expectedQuantity))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, đội {actualGroup.Key.TeamId} đang trả thêm item '{ResolveItemName(actualItem.Key, itemLookup)}' không xuất hiện trong COLLECT_SUPPLIES reusable tương ứng.");
                    continue;
                }

                if (actualItem.Value != expectedQuantity)
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {actualGroup.Key.DepotId}, đội {actualGroup.Key.TeamId} phải trả đúng {expectedQuantity} đơn vị item '{ResolveItemName(actualItem.Key, itemLookup)}', nhưng payload hiện có {actualItem.Value}.");
                }
            }
        }

        foreach (var expectedGroup in requiredReturnItems)
        {
            if (!actualReturnItems.TryGetValue(expectedGroup.Key, out var actualItems))
            {
                errors.Add(
                    $"Thiếu RETURN_SUPPLIES cuối kế hoạch cho kho {expectedGroup.Key.DepotId}, đội {expectedGroup.Key.TeamId} dù đã COLLECT_SUPPLIES vật tư reusable.");
                continue;
            }

            foreach (var expectedItem in expectedGroup.Value)
            {
                if (!actualItems.ContainsKey(expectedItem.Key))
                {
                    errors.Add(
                        $"RETURN_SUPPLIES cho kho {expectedGroup.Key.DepotId}, đội {expectedGroup.Key.TeamId} chưa trả item '{ResolveItemName(expectedItem.Key, itemLookup)}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new BadRequestException($"Kế hoạch mission chưa hợp lệ với vật tư reusable:\n{string.Join("\n", errors)}");
        }
    }

    private async Task NotifyTeamMembersAsync(List<CreateActivityItemDto> activities, int missionId, CancellationToken cancellationToken)
    {
        var teamIds = activities
            .Where(a => a.RescueTeamId.HasValue)
            .Select(a => a.RescueTeamId!.Value)
            .Distinct()
            .ToList();

        foreach (var teamId in teamIds)
        {
            try
            {
                var team = await _rescueTeamRepository.GetByIdAsync(teamId, cancellationToken);
                if (team?.Members is null) continue;

                foreach (var member in team.Members)
                {
                    try
                    {
                        await _firebaseService.SendNotificationToUserAsync(
                            member.UserId,
                            "Nhiệm vụ mới",
                            $"Đội của bạn đã được phân công vào nhiệm vụ #{missionId}. Vui lòng kiểm tra chi tiết.",
                            "mission_assigned",
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to send notification to UserId={userId} for MissionId={missionId}",
                            member.UserId, missionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch team or notify members for RescueTeamId={teamId}, MissionId={missionId}",
                    teamId, missionId);
            }
        }
    }

    private async Task ReserveSuppliesAsync(
        List<CreateActivityItemDto> requestActivities,
        List<MissionActivityModel> persistedActivities,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < Math.Min(requestActivities.Count, persistedActivities.Count); index++)
        {
            var requestActivity = requestActivities[index];
            var persistedActivity = persistedActivities[index];

            if (!IsCollectSuppliesActivity(requestActivity)
                || !requestActivity.DepotId.HasValue
                || requestActivity.SuppliesToCollect is not { Count: > 0 })
            {
                continue;
            }

            var itemsToReserve = requestActivity.SuppliesToCollect
                .Where(s => s.Id.HasValue && (s.Quantity ?? 0) > 0)
                .Select(s =>
                {
                    var bufferRatio = Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
                    var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling((s.Quantity ?? 0) * bufferRatio) : 0;
                    return (ItemModelId: s.Id!.Value, Quantity: (s.Quantity ?? 0) + bufferQty);
                })
                .ToList();

            if (itemsToReserve.Count == 0)
                continue;

            try
            {
                var reservationResult = await _depotInventoryRepository.ReserveSuppliesAsync(
                    requestActivity.DepotId.Value,
                    itemsToReserve,
                    cancellationToken);

                await MissionSupplyExecutionSnapshotHelper.SyncReservationSnapshotAsync(
                    persistedActivity,
                    reservationResult,
                    _missionActivityRepository,
                    _logger,
                    cancellationToken);
                await _unitOfWork.SaveAsync();
            }
            catch (InvalidOperationException ex)
            {
                var activityLabel = persistedActivity.Step.HasValue
                    ? $"bước {persistedActivity.Step.Value}"
                    : $"hoạt động #{persistedActivity.Id}";

                throw new BadRequestException(
                    $"Không thể tạo mission vì kho #{requestActivity.DepotId.Value} không đặt trước được vật tư cho {activityLabel}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Không thể đặt trước vật tư tại kho {DepotId} cho activity #{ActivityId} khi tạo mission",
                    requestActivity.DepotId.Value,
                    persistedActivity.Id);
                throw;
            }
        }
    }

    private static bool IsCollectSuppliesActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnSuppliesActivity(CreateActivityItemDto activity) =>
        string.Equals(activity.ActivityType, ReturnSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReusableItem(
        int itemId,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup) =>
        itemLookup.TryGetValue(itemId, out var item)
        && string.Equals(item.ItemType, ReusableItemType, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<int, int> GetOrCreateItemBucket(
        IDictionary<(int DepotId, int TeamId), Dictionary<int, int>> buckets,
        (int DepotId, int TeamId) key)
    {
        if (!buckets.TryGetValue(key, out var bucket))
        {
            bucket = [];
            buckets[key] = bucket;
        }

        return bucket;
    }

    private static string ResolveItemName(
        int itemId,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup,
        string? fallbackName = null)
    {
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName;

        return itemLookup.TryGetValue(itemId, out var item)
            ? item.Name
            : $"Item#{itemId}";
    }
}
