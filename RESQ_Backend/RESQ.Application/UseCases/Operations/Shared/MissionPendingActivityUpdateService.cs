using System.Text.Json;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.UpdateMission;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

public class MissionPendingActivityUpdateService(
    IMissionActivityRepository activityRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IUnitOfWork unitOfWork,
    ILogger<MissionPendingActivityUpdateService> logger
) : IMissionPendingActivityUpdateService
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IAssemblyPointRepository _assemblyPointRepository = assemblyPointRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<MissionPendingActivityUpdateService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const double DefaultBufferRatio = 0.10;
    private const string ReturnAssemblyPointActivityType = "RETURN_ASSEMBLY_POINT";

    public async Task ApplyAsync(
        MissionModel mission,
        Guid updatedBy,
        IReadOnlyList<UpdateMissionActivityPatch> activities,
        CancellationToken cancellationToken = default)
    {
        if (activities.Count == 0)
            return;

        _logger.LogInformation(
            "Updating {Count} pending mission activities in MissionId={MissionId}",
            activities.Count,
            mission.Id);

        if (mission.Status is MissionStatus.Completed or MissionStatus.Incompleted)
            throw new BadRequestException("Không thể cập nhật activity của mission đã kết thúc.");

        var missionActivities = (await _activityRepository.GetByMissionIdAsync(mission.Id, cancellationToken)).ToList();
        var activityLookup = missionActivities.ToDictionary(activity => activity.Id);
        var plans = new List<ActivityUpdatePlan>(activities.Count);

        foreach (var patch in activities)
        {
            if (!activityLookup.TryGetValue(patch.ActivityId, out var activity))
                throw new NotFoundException($"Activity #{patch.ActivityId} không thuộc mission #{mission.Id}.");

            var isOngoingReturnAssemblyPointUpdate = IsOngoingReturnAssemblyPoint(activity);
            if (activity.Status != MissionActivityStatus.Planned
                && !CanUpdateOngoingReturnAssemblyPoint(activity, patch))
            {
                throw new BadRequestException(
                    $"Chỉ được cập nhật activity Planned. Activity #{activity.Id} hiện ở trạng thái {activity.Status}.");
            }

            var currentItems = ParseSupplies(activity.Items);
            var nextAssemblyPoint = patch.AssemblyPointId.HasValue
                ? await GetAssemblyPointForUpdateAsync(patch.AssemblyPointId.Value, cancellationToken)
                : null;
            var shouldReplaceItems = patch.Items is not null
                && !(isOngoingReturnAssemblyPointUpdate && AreSuppliesEquivalent(currentItems, patch.Items));
            IReadOnlyList<SupplyToCollectDto> nextItems = !shouldReplaceItems
                ? []
                : NormalizeRequestedSupplies(patch.Items!, currentItems);
            var nextItemsJson = !shouldReplaceItems
                ? activity.Items
                : nextItems.Count == 0
                    ? null
                    : JsonSerializer.Serialize(nextItems);

            var projectedActivity = CloneActivity(activity);
            ApplyPatch(projectedActivity, patch, nextItemsJson, nextAssemblyPoint, updatedBy);

            plans.Add(new ActivityUpdatePlan(activity, projectedActivity, patch, currentItems, nextItems, shouldReplaceItems));
        }

        ValidateProjectedSteps(missionActivities, plans);
        await ValidateInventoryAvailabilityAsync(plans, cancellationToken);

        foreach (var plan in plans.Where(plan => plan.ShouldReplaceItems && plan.Activity.DepotId.HasValue))
        {
            var itemsToRelease = BuildReservationItems(plan.CurrentItems);
            if (itemsToRelease.Count == 0)
                continue;

            await _depotInventoryRepository.ReleaseReservedSuppliesAsync(
                plan.Activity.DepotId!.Value,
                itemsToRelease,
                cancellationToken);
        }

        foreach (var plan in plans)
        {
            CopyProjectedValues(plan.Activity, plan.ProjectedActivity);
            await _activityRepository.UpdateAsync(plan.Activity, cancellationToken);
        }

        await _unitOfWork.SaveAsync();

        foreach (var plan in plans)
        {
            if (plan.ShouldReplaceItems && plan.Activity.DepotId.HasValue && plan.NextItems.Count > 0)
            {
                var reservationResult = await _depotInventoryRepository.ReserveSuppliesAsync(
                    plan.Activity.DepotId.Value,
                    BuildReservationItems(plan.NextItems),
                    cancellationToken);

                await MissionSupplyExecutionSnapshotHelper.SyncReservationSnapshotAsync(
                    plan.Activity,
                    reservationResult,
                    _activityRepository,
                    _logger,
                    cancellationToken);
                await _unitOfWork.SaveAsync();
                continue;
            }

            if (plan.ShouldReplaceItems)
            {
                await MissionSupplyExecutionSnapshotHelper.RebuildExpectedReturnUnitsAsync(
                    plan.Activity,
                    _activityRepository,
                    _logger,
                    cancellationToken);
            }
        }

        await _unitOfWork.SaveAsync();
    }

    private static void ValidateProjectedSteps(
        IReadOnlyCollection<MissionActivityModel> missionActivities,
        IReadOnlyCollection<ActivityUpdatePlan> plans)
    {
        var projectedById = missionActivities.ToDictionary(activity => activity.Id, CloneActivity);
        foreach (var plan in plans)
        {
            projectedById[plan.Activity.Id] = CloneActivity(plan.ProjectedActivity);
        }

        foreach (var teamGroup in projectedById.Values.GroupBy(activity => activity.MissionTeamId))
        {
            var duplicateStep = teamGroup
                .Where(activity => activity.Step.HasValue)
                .GroupBy(activity => activity.Step!.Value)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateStep is null)
                continue;

            var missionTeamLabel = teamGroup.Key.HasValue
                ? $"đội #{teamGroup.Key.Value}"
                : "nhóm activity chưa gán đội";
            var activityIds = string.Join(", ", duplicateStep.Select(activity => $"#{activity.Id}"));
            throw new BadRequestException(
                $"Step {duplicateStep.Key} bị trùng trong {missionTeamLabel}. Các activity liên quan: {activityIds}.");
        }
    }

    private async Task ValidateInventoryAvailabilityAsync(
        IReadOnlyCollection<ActivityUpdatePlan> plans,
        CancellationToken cancellationToken)
    {
        var deltasByDepot = new Dictionary<int, Dictionary<int, InventoryDelta>>();

        foreach (var plan in plans.Where(plan => plan.ShouldReplaceItems && plan.Activity.DepotId.HasValue))
        {
            var depotId = plan.Activity.DepotId!.Value;
            var oldItems = BuildReservationItems(plan.CurrentItems);
            var newItems = BuildReservationItems(plan.NextItems);
            var oldLookup = oldItems.ToDictionary(item => item.ItemModelId, item => item.Quantity);
            var newLookup = newItems.ToDictionary(item => item.ItemModelId, item => item.Quantity);

            foreach (var itemModelId in oldLookup.Keys.Union(newLookup.Keys))
            {
                var oldQuantity = oldLookup.GetValueOrDefault(itemModelId);
                var newQuantity = newLookup.GetValueOrDefault(itemModelId);
                var delta = newQuantity - oldQuantity;
                if (delta <= 0)
                    continue;

                if (!deltasByDepot.TryGetValue(depotId, out var depotDeltas))
                {
                    depotDeltas = [];
                    deltasByDepot[depotId] = depotDeltas;
                }

                if (!depotDeltas.TryGetValue(itemModelId, out var inventoryDelta))
                {
                    inventoryDelta = new InventoryDelta($"Item#{itemModelId}", 0);
                }

                var itemName = plan.NextItems.FirstOrDefault(item => item.ItemId == itemModelId)?.ItemName
                    ?? plan.CurrentItems.FirstOrDefault(item => item.ItemId == itemModelId)?.ItemName
                    ?? inventoryDelta.ItemName;

                depotDeltas[itemModelId] = inventoryDelta with
                {
                    ItemName = itemName,
                    Quantity = inventoryDelta.Quantity + delta
                };
            }
        }

        foreach (var depot in deltasByDepot)
        {
            var itemsToCheck = depot.Value
                .Select(item => (item.Key, item.Value.ItemName, item.Value.Quantity))
                .Where(item => item.Quantity > 0)
                .ToList();

            if (itemsToCheck.Count == 0)
                continue;

            var shortages = await _depotInventoryRepository.CheckSupplyAvailabilityAsync(
                depot.Key,
                itemsToCheck,
                cancellationToken);

            if (shortages.Count == 0)
                continue;

            var errors = shortages.Select(shortage => shortage.NotFound
                ? $"Kho {depot.Key}: vật phẩm '{shortage.ItemName}' không có trong kho."
                : $"Kho {depot.Key}: vật phẩm '{shortage.ItemName}' không đủ số lượng bổ sung - cần thêm {shortage.RequestedQuantity}, khả dụng {shortage.AvailableQuantity}.");
            throw new BadRequestException($"Kiểm tra tồn kho thất bại:\n{string.Join("\n", errors)}");
        }
    }

    private async Task<AssemblyPointModel> GetAssemblyPointForUpdateAsync(
        int assemblyPointId,
        CancellationToken cancellationToken)
    {
        var assemblyPoint = await _assemblyPointRepository.GetByIdAsync(assemblyPointId, cancellationToken)
            ?? throw new BadRequestException($"Khong tim thay diem tap ket #{assemblyPointId}.");

        if (assemblyPoint.Location is null)
            throw new BadRequestException($"Diem tap ket #{assemblyPointId} chua co toa do hop le.");

        return assemblyPoint;
    }

    private static bool CanUpdateOngoingReturnAssemblyPoint(
        MissionActivityModel activity,
        UpdateMissionActivityPatch patch)
    {
        if (!IsOngoingReturnAssemblyPoint(activity))
            return false;

        return IsSameStep(activity, patch)
            && IsSameTarget(activity, patch)
            && IsSameCoordinates(activity, patch)
            && IsSameItems(activity, patch);
    }

    private static bool IsOngoingReturnAssemblyPoint(MissionActivityModel activity)
    {
        return string.Equals(activity.ActivityType, ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase)
            && activity.Status == MissionActivityStatus.OnGoing;
    }

    private static bool IsSameStep(MissionActivityModel activity, UpdateMissionActivityPatch patch) =>
        !patch.Step.HasValue || patch.Step == activity.Step;

    private static bool IsSameTarget(MissionActivityModel activity, UpdateMissionActivityPatch patch) =>
        patch.Target is null || string.Equals(patch.Target, activity.Target, StringComparison.Ordinal);

    private static bool IsSameCoordinates(MissionActivityModel activity, UpdateMissionActivityPatch patch)
    {
        return (!patch.TargetLatitude.HasValue || AreSameCoordinate(patch.TargetLatitude.Value, activity.TargetLatitude))
            && (!patch.TargetLongitude.HasValue || AreSameCoordinate(patch.TargetLongitude.Value, activity.TargetLongitude));
    }

    private static bool IsSameItems(MissionActivityModel activity, UpdateMissionActivityPatch patch) =>
        patch.Items is null || AreSuppliesEquivalent(ParseSupplies(activity.Items), patch.Items);

    private static bool AreSameCoordinate(double provided, double? existing) =>
        existing.HasValue && Math.Abs(provided - existing.Value) < 0.000001;

    private static bool AreSuppliesEquivalent(
        IReadOnlyCollection<SupplyToCollectDto> currentItems,
        IReadOnlyCollection<SupplyToCollectDto> requestedItems)
    {
        var current = currentItems
            .OrderBy(item => item.ItemId)
            .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
            .Select(NormalizeSupplyForComparison)
            .ToList();
        var requested = requestedItems
            .OrderBy(item => item.ItemId)
            .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
            .Select(NormalizeSupplyForComparison)
            .ToList();

        return current.Count == requested.Count
            && current.Zip(requested).All(pair => pair.First == pair.Second);
    }

    private static SupplyComparison NormalizeSupplyForComparison(SupplyToCollectDto item) =>
        new(
            item.ItemId,
            item.ItemName?.Trim() ?? string.Empty,
            item.Quantity,
            item.Unit?.Trim() ?? string.Empty,
            item.BufferRatio,
            item.BufferQuantity);

    private static MissionActivityModel CloneActivity(MissionActivityModel activity) => new()
    {
        Id = activity.Id,
        MissionId = activity.MissionId,
        Step = activity.Step,
        ActivityType = activity.ActivityType,
        Description = activity.Description,
        Target = activity.Target,
        Items = activity.Items,
        TargetLatitude = activity.TargetLatitude,
        TargetLongitude = activity.TargetLongitude,
        Status = activity.Status,
        MissionTeamId = activity.MissionTeamId,
        Priority = activity.Priority,
        EstimatedTime = activity.EstimatedTime,
        SosRequestId = activity.SosRequestId,
        DepotId = activity.DepotId,
        DepotName = activity.DepotName,
        DepotAddress = activity.DepotAddress,
        AssemblyPointId = activity.AssemblyPointId,
        AssemblyPointName = activity.AssemblyPointName,
        AssemblyPointLatitude = activity.AssemblyPointLatitude,
        AssemblyPointLongitude = activity.AssemblyPointLongitude,
        AssignedAt = activity.AssignedAt,
        CompletedAt = activity.CompletedAt,
        LastDecisionBy = activity.LastDecisionBy,
        CompletedBy = activity.CompletedBy
    };

    private static void ApplyPatch(
        MissionActivityModel activity,
        UpdateMissionActivityPatch patch,
        string? nextItemsJson,
        AssemblyPointModel? nextAssemblyPoint,
        Guid updatedBy)
    {
        if (patch.Step.HasValue)
            activity.Step = patch.Step.Value;

        if (patch.Description is not null)
            activity.Description = patch.Description;

        if (patch.Target is not null)
            activity.Target = patch.Target;

        if (patch.TargetLatitude.HasValue && patch.TargetLongitude.HasValue)
        {
            activity.TargetLatitude = patch.TargetLatitude.Value;
            activity.TargetLongitude = patch.TargetLongitude.Value;
        }

        if (patch.Items is not null)
            activity.Items = nextItemsJson;

        if (nextAssemblyPoint is not null)
        {
            activity.AssemblyPointId = nextAssemblyPoint.Id;
            activity.AssemblyPointName = nextAssemblyPoint.Name;
            activity.AssemblyPointLatitude = nextAssemblyPoint.Location!.Latitude;
            activity.AssemblyPointLongitude = nextAssemblyPoint.Location.Longitude;
        }

        activity.LastDecisionBy = updatedBy;
    }

    private static void CopyProjectedValues(MissionActivityModel target, MissionActivityModel source)
    {
        target.Step = source.Step;
        target.Description = source.Description;
        target.Target = source.Target;
        target.Items = source.Items;
        target.TargetLatitude = source.TargetLatitude;
        target.TargetLongitude = source.TargetLongitude;
        target.AssemblyPointId = source.AssemblyPointId;
        target.AssemblyPointName = source.AssemblyPointName;
        target.AssemblyPointLatitude = source.AssemblyPointLatitude;
        target.AssemblyPointLongitude = source.AssemblyPointLongitude;
        target.LastDecisionBy = source.LastDecisionBy;
    }

    private static List<SupplyToCollectDto> ParseSupplies(string? itemsJson) =>
        string.IsNullOrWhiteSpace(itemsJson)
            ? []
            : JsonSerializer.Deserialize<List<SupplyToCollectDto>>(itemsJson, JsonOpts) ?? [];

    private static List<SupplyToCollectDto> NormalizeRequestedSupplies(
        IEnumerable<SupplyToCollectDto> requestedItems,
        IReadOnlyCollection<SupplyToCollectDto> currentItems)
    {
        return requestedItems
            .Where(item => item.ItemId.HasValue)
            .Select(item =>
            {
                var existing = currentItems.FirstOrDefault(current => current.ItemId == item.ItemId);
                var quantity = item.Quantity;
                var bufferRatio = Math.Max(0.0, item.BufferRatio ?? existing?.BufferRatio ?? DefaultBufferRatio);
                var bufferQuantity = bufferRatio > 0 ? (int)Math.Ceiling(quantity * bufferRatio) : 0;

                return new SupplyToCollectDto
                {
                    ItemId = item.ItemId,
                    ItemName = string.IsNullOrWhiteSpace(item.ItemName)
                        ? existing?.ItemName ?? $"Item#{item.ItemId}"
                        : item.ItemName,
                    ImageUrl = item.ImageUrl ?? existing?.ImageUrl,
                    Quantity = quantity,
                    Unit = item.Unit ?? existing?.Unit,
                    BufferRatio = bufferRatio > 0 ? bufferRatio : null,
                    BufferQuantity = bufferQuantity > 0 ? bufferQuantity : null
                };
            })
            .ToList();
    }

    private static List<(int ItemModelId, int Quantity)> BuildReservationItems(IEnumerable<SupplyToCollectDto> items) =>
        items
            .Where(item => item.ItemId.HasValue && item.Quantity > 0)
            .Select(item => (item.ItemId!.Value, item.Quantity + (item.BufferQuantity ?? 0)))
            .ToList();

    private sealed record ActivityUpdatePlan(
        MissionActivityModel Activity,
        MissionActivityModel ProjectedActivity,
        UpdateMissionActivityPatch Patch,
        IReadOnlyList<SupplyToCollectDto> CurrentItems,
        IReadOnlyList<SupplyToCollectDto> NextItems,
        bool ShouldReplaceItems);

    private sealed record InventoryDelta(string ItemName, int Quantity);
    private sealed record SupplyComparison(
        int? ItemId,
        string ItemName,
        int Quantity,
        string Unit,
        double? BufferRatio,
        int? BufferQuantity);
}
