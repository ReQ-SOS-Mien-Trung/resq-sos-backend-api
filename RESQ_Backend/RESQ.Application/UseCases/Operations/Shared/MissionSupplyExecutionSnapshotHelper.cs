using System.Text.Json;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionSupplyExecutionSnapshotHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task SyncReservationSnapshotAsync(
        MissionActivityModel collectActivity,
        MissionSupplyReservationResult reservationResult,
        IMissionActivityRepository activityRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(collectActivity.Items))
            return;

        var collectSupplies = DeserializeSupplies(collectActivity.Items);
        if (collectSupplies.Count == 0)
            return;

        ApplyReservationSnapshot(collectSupplies, reservationResult.Items);
        collectActivity.Items = JsonSerializer.Serialize(collectSupplies);
        await activityRepository.UpdateAsync(collectActivity, cancellationToken);

        await RebuildExpectedReturnUnitsAsync(collectActivity, activityRepository, logger, cancellationToken);
    }

    public static async Task SyncPickupExecutionAsync(
        MissionActivityModel collectActivity,
        MissionSupplyPickupExecutionResult pickupResult,
        IMissionActivityRepository activityRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (pickupResult.Items.Count == 0 || string.IsNullOrWhiteSpace(collectActivity.Items))
            return;

        var collectSupplies = DeserializeSupplies(collectActivity.Items);
        if (collectSupplies.Count == 0)
            return;

        ApplyPickupExecution(collectSupplies, pickupResult.Items);
        collectActivity.Items = JsonSerializer.Serialize(collectSupplies);
        await activityRepository.UpdateAsync(collectActivity, cancellationToken);

        await RebuildExpectedReturnUnitsAsync(collectActivity, activityRepository, logger, cancellationToken);
    }

    public static async Task RebuildExpectedReturnUnitsAsync(
        MissionActivityModel referenceActivity,
        IMissionActivityRepository activityRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!referenceActivity.MissionId.HasValue || !referenceActivity.MissionTeamId.HasValue || !referenceActivity.DepotId.HasValue)
            return;

        var missionActivities = (await activityRepository.GetByMissionIdAsync(referenceActivity.MissionId.Value, cancellationToken)).ToList();
        var returnActivity = missionActivities.FirstOrDefault(activity =>
            activity.MissionTeamId == referenceActivity.MissionTeamId
            && activity.DepotId == referenceActivity.DepotId
            && string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(activity.Items));

        if (returnActivity is null)
            return;

        var expectedByItem = missionActivities
            .Where(activity => activity.MissionTeamId == referenceActivity.MissionTeamId
                && activity.DepotId == referenceActivity.DepotId
                && string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(activity.Items))
            .SelectMany(activity => DeserializeSupplies(activity.Items!))
            .Where(supply => supply.ItemId.HasValue)
            .GroupBy(supply => supply.ItemId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(GetExpectedReturnSourceUnits)
                    .GroupBy(unit => unit.ReusableItemId)
                    .Select(unitGroup => CloneReusableUnit(unitGroup.First()))
                    .OrderBy(unit => unit.ReusableItemId)
                    .ToList());

        var returnSupplies = DeserializeSupplies(returnActivity.Items!);
        var changed = false;
        foreach (var supply in returnSupplies)
        {
            if (!supply.ItemId.HasValue)
                continue;

            expectedByItem.TryGetValue(supply.ItemId.Value, out var expectedUnits);
            expectedUnits ??= [];

            var currentUnits = supply.ExpectedReturnUnits?
                .OrderBy(unit => unit.ReusableItemId)
                .ToList() ?? [];
            if (!AreSameUnits(currentUnits, expectedUnits))
            {
                supply.ExpectedReturnUnits = expectedUnits.Count == 0 ? null : expectedUnits;
                changed = true;
            }
        }

        if (!changed)
            return;

        returnActivity.Items = JsonSerializer.Serialize(returnSupplies);
        await activityRepository.UpdateAsync(returnActivity, cancellationToken);

        logger.LogInformation(
            "Rebuilt expected return units for ReturnActivityId={ReturnActivityId} from collect activities of MissionTeamId={MissionTeamId} DepotId={DepotId}",
            returnActivity.Id,
            referenceActivity.MissionTeamId,
            referenceActivity.DepotId);
    }

    public static async Task PersistReturnExecutionAsync(
        MissionActivityModel returnActivity,
        MissionSupplyReturnExecutionResult returnResult,
        string? discrepancyNote,
        IMissionActivityRepository activityRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(returnActivity.Items))
            return;

        var supplies = DeserializeSupplies(returnActivity.Items);
        if (supplies.Count == 0)
            return;

        ApplyReturnExecution(supplies, returnResult.Items);
        returnActivity.Items = JsonSerializer.Serialize(supplies);

        if (!string.IsNullOrWhiteSpace(discrepancyNote))
        {
            var prefix = "[Return discrepancy] ";
            var suffix = prefix + discrepancyNote.Trim();
            returnActivity.Description = string.IsNullOrWhiteSpace(returnActivity.Description)
                ? suffix
                : returnActivity.Description.Contains(suffix, StringComparison.Ordinal)
                    ? returnActivity.Description
                    : $"{returnActivity.Description}{Environment.NewLine}{suffix}";
        }

        await activityRepository.UpdateAsync(returnActivity, cancellationToken);
    }

    /// <summary>
    /// Ghi nhận thông tin sử dụng buffer (số lượng + lý do) vào JSONB snapshot của activity.
    /// Phải được gọi trước khi activity chuyển sang Succeed để số buffer được tính vào lượng consume.
    /// </summary>
    public static async Task SyncBufferUsageAsync(
        MissionActivityModel collectActivity,
        Dictionary<int, MissionPickupBufferUsageDto> bufferUsageByItemId,
        IMissionActivityRepository activityRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(collectActivity.Items))
            return;

        var supplies = DeserializeSupplies(collectActivity.Items);
        if (supplies.Count == 0)
            return;

        var changed = false;
        foreach (var supply in supplies)
        {
            if (!supply.ItemId.HasValue || !bufferUsageByItemId.TryGetValue(supply.ItemId.Value, out var usage))
                continue;

            supply.BufferUsedQuantity = usage.BufferQuantityUsed > 0 ? usage.BufferQuantityUsed : (int?)null;
            supply.BufferUsedReason = usage.BufferQuantityUsed > 0 ? usage.BufferUsedReason : null;
            changed = true;
        }

        if (!changed)
            return;

        collectActivity.Items = JsonSerializer.Serialize(supplies);
        await activityRepository.UpdateAsync(collectActivity, cancellationToken);
    }

    private static List<SupplyToCollectDto> DeserializeSupplies(string itemsJson) =>
        JsonSerializer.Deserialize<List<SupplyToCollectDto>>(itemsJson, JsonOpts) ?? [];

    private static void ApplyReservationSnapshot(
        IEnumerable<SupplyToCollectDto> supplies,
        IEnumerable<SupplyExecutionItemDto> reservationItems)
    {
        var reservationLookup = reservationItems.ToDictionary(item => item.ItemModelId);

        foreach (var supply in supplies)
        {
            if (!supply.ItemId.HasValue || !reservationLookup.TryGetValue(supply.ItemId.Value, out var reservationItem))
                continue;

            supply.PlannedPickupLotAllocations = reservationItem.LotAllocations.Count == 0
                ? null
                : reservationItem.LotAllocations.Select(CloneLot).ToList();
            supply.PlannedPickupReusableUnits = reservationItem.ReusableUnits.Count == 0
                ? null
                : reservationItem.ReusableUnits.Select(CloneReusableUnit).ToList();
        }
    }

    private static void ApplyPickupExecution(
        IEnumerable<SupplyToCollectDto> supplies,
        IEnumerable<SupplyExecutionItemDto> executionItems)
    {
        var executionLookup = executionItems.ToDictionary(item => item.ItemModelId);

        foreach (var supply in supplies)
        {
            if (!supply.ItemId.HasValue || !executionLookup.TryGetValue(supply.ItemId.Value, out var executionItem))
                continue;

            supply.PickupLotAllocations = executionItem.LotAllocations.Count == 0
                ? null
                : executionItem.LotAllocations.Select(CloneLot).ToList();
            supply.PickedReusableUnits = executionItem.ReusableUnits.Count == 0
                ? null
                : executionItem.ReusableUnits.Select(CloneReusableUnit).ToList();
        }
    }

    private static void ApplyReturnExecution(
        IEnumerable<SupplyToCollectDto> supplies,
        IEnumerable<MissionSupplyReturnExecutionItemDto> executionItems)
    {
        var executionLookup = executionItems.ToDictionary(item => item.ItemModelId);

        foreach (var supply in supplies)
        {
            if (!supply.ItemId.HasValue || !executionLookup.TryGetValue(supply.ItemId.Value, out var executionItem))
                continue;

            supply.ActualReturnedQuantity = executionItem.ActualQuantity;
            supply.ReturnedReusableUnits = executionItem.ReturnedReusableUnits.Count == 0
                ? null
                : executionItem.ReturnedReusableUnits.Select(CloneReusableUnit).ToList();
        }
    }

    private static IEnumerable<SupplyExecutionReusableUnitDto> GetExpectedReturnSourceUnits(SupplyToCollectDto supply)
    {
        if (supply.PickedReusableUnits is { Count: > 0 })
            return supply.PickedReusableUnits.Select(CloneReusableUnit);

        if (supply.PlannedPickupReusableUnits is { Count: > 0 })
            return supply.PlannedPickupReusableUnits.Select(CloneReusableUnit);

        return [];
    }

    private static bool AreSameUnits(
        IReadOnlyList<SupplyExecutionReusableUnitDto> left,
        IReadOnlyList<SupplyExecutionReusableUnitDto> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].ReusableItemId != right[index].ReusableItemId)
                return false;
        }

        return true;
    }

    private static SupplyExecutionLotDto CloneLot(SupplyExecutionLotDto lot) => new()
    {
        LotId = lot.LotId,
        QuantityTaken = lot.QuantityTaken,
        ReceivedDate = lot.ReceivedDate,
        ExpiredDate = lot.ExpiredDate,
        RemainingQuantityAfterExecution = lot.RemainingQuantityAfterExecution
    };

    private static SupplyExecutionReusableUnitDto CloneReusableUnit(SupplyExecutionReusableUnitDto unit) => new()
    {
        ReusableItemId = unit.ReusableItemId,
        ItemModelId = unit.ItemModelId,
        ItemName = unit.ItemName,
        SerialNumber = unit.SerialNumber,
        Condition = unit.Condition
    };
}
