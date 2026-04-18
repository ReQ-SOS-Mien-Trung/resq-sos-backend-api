using System.Text.Json;
using RESQ.Application.Common.Models;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionSupplyCarriedBalanceHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static MissionSupplyCarriedBalance CalculateBeforeActivity(
        IEnumerable<MissionActivityModel> missionActivities,
        MissionActivityModel referenceActivity,
        bool subtractPlannedDeliveries = true)
    {
        var balance = new MissionSupplyCarriedBalance();
        var orderedActivities = missionActivities
            .Where(activity => activity.MissionTeamId == referenceActivity.MissionTeamId)
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .ToList();

        foreach (var activity in orderedActivities)
        {
            if (!IsBefore(activity, referenceActivity))
                break;

            ApplyActivity(balance, activity, subtractPlannedDeliveries);
        }

        return balance;
    }

    public static MissionSupplyCarriedBalance CalculateAtEndForTeam(
        IEnumerable<MissionActivityModel> missionActivities,
        int? missionTeamId,
        bool subtractPlannedDeliveries = true)
    {
        var balance = new MissionSupplyCarriedBalance();
        foreach (var activity in missionActivities
            .Where(activity => activity.MissionTeamId == missionTeamId)
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id))
        {
            ApplyActivity(balance, activity, subtractPlannedDeliveries);
        }

        return balance;
    }

    public static List<SupplyToCollectDto> BuildExpectedReturnSupplies(
        MissionActivityModel returnActivity,
        IEnumerable<MissionActivityModel> missionActivities,
        IEnumerable<SupplyToCollectDto>? currentReturnSupplies)
    {
        var balance = CalculateBeforeActivity(
            missionActivities,
            returnActivity,
            subtractPlannedDeliveries: true);

        return BuildSuppliesFromBalance(balance, returnActivity.DepotId, currentReturnSupplies);
    }

    public static List<SupplyToCollectDto> BuildSuppliesFromBalance(
        MissionSupplyCarriedBalance balance,
        int? sourceDepotId,
        IEnumerable<SupplyToCollectDto>? currentSupplies = null)
    {
        var currentLookup = (currentSupplies ?? [])
            .Where(supply => supply.ItemId.HasValue)
            .GroupBy(supply => supply.ItemId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var itemIds = balance.GetItemIds(sourceDepotId)
            .OrderBy(itemId => itemId)
            .ToList();

        var result = new List<SupplyToCollectDto>();
        foreach (var itemId in itemIds)
        {
            currentLookup.TryGetValue(itemId, out var current);
            var lots = balance.GetLots(itemId, sourceDepotId);
            var units = balance.GetReusableUnits(itemId, sourceDepotId);
            var summary = balance.GetItemSummary(itemId, sourceDepotId);
            var expectedQuantity = lots.Sum(lot => lot.QuantityTaken) + units.Count;

            if (expectedQuantity <= 0)
                continue;

            result.Add(new SupplyToCollectDto
            {
                ItemId = itemId,
                ItemName = current?.ItemName ?? summary.ItemName ?? $"Item#{itemId}",
                ImageUrl = current?.ImageUrl,
                Quantity = expectedQuantity,
                Unit = current?.Unit ?? summary.Unit,
                PlannedPickupLotAllocations = current?.PlannedPickupLotAllocations,
                PlannedPickupReusableUnits = current?.PlannedPickupReusableUnits,
                PickupLotAllocations = current?.PickupLotAllocations,
                PickedReusableUnits = current?.PickedReusableUnits,
                AvailableDeliveryLotAllocations = current?.AvailableDeliveryLotAllocations,
                AvailableDeliveryReusableUnits = current?.AvailableDeliveryReusableUnits,
                DeliveredLotAllocations = current?.DeliveredLotAllocations,
                DeliveredReusableUnits = current?.DeliveredReusableUnits,
                ExpectedReturnLotAllocations = lots.Count == 0 ? null : lots,
                ExpectedReturnUnits = units.Count == 0 ? null : units,
                ReturnedLotAllocations = current?.ReturnedLotAllocations,
                ReturnedReusableUnits = current?.ReturnedReusableUnits,
                ActualReturnedQuantity = current?.ActualReturnedQuantity,
                BufferRatio = current?.BufferRatio,
                BufferQuantity = current?.BufferQuantity,
                BufferUsedQuantity = current?.BufferUsedQuantity,
                BufferUsedReason = current?.BufferUsedReason,
                ActualDeliveredQuantity = current?.ActualDeliveredQuantity
            });
        }

        return result;
    }

    public static List<SupplyToCollectDto> ParseSupplies(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
            return [];

        return JsonSerializer.Deserialize<List<SupplyToCollectDto>>(itemsJson, JsonOpts) ?? [];
    }

    private static void ApplyActivity(
        MissionSupplyCarriedBalance balance,
        MissionActivityModel activity,
        bool subtractPlannedDeliveries)
    {
        var supplies = ParseSupplies(activity.Items);
        if (supplies.Count == 0)
            return;

        if (IsActivity(activity, "COLLECT_SUPPLIES"))
        {
            foreach (var supply in supplies.Where(supply => supply.ItemId.HasValue))
            {
                var itemId = supply.ItemId!.Value;
                var lots = supply.PickupLotAllocations is { Count: > 0 }
                    ? supply.PickupLotAllocations
                    : supply.PlannedPickupLotAllocations;
                foreach (var lot in lots ?? [])
                {
                    if (lot.QuantityTaken > 0)
                        balance.AddLot(itemId, activity.DepotId, supply.ItemName, supply.Unit, lot);
                }

                var units = supply.PickedReusableUnits is { Count: > 0 }
                    ? supply.PickedReusableUnits
                    : supply.PlannedPickupReusableUnits;
                foreach (var unit in units ?? [])
                {
                    if (unit.ReusableItemId > 0)
                        balance.AddReusableUnit(itemId, activity.DepotId, supply.ItemName, supply.Unit, unit);
                }
            }

            return;
        }

        if (IsActivity(activity, "DELIVER_SUPPLIES"))
        {
            foreach (var supply in supplies.Where(supply => supply.ItemId.HasValue))
            {
                var itemId = supply.ItemId!.Value;
                if (supply.DeliveredLotAllocations is { Count: > 0 })
                {
                    foreach (var lot in supply.DeliveredLotAllocations)
                        balance.RemoveLot(itemId, lot.LotId, lot.QuantityTaken);
                }
                else if (supply.DeliveredReusableUnits is { Count: > 0 })
                {
                    foreach (var unit in supply.DeliveredReusableUnits)
                        balance.RemoveReusableUnit(itemId, unit.ReusableItemId);
                }
                else if (supply.ActualDeliveredQuantity.HasValue || subtractPlannedDeliveries)
                {
                    var quantity = Math.Max(0, supply.ActualDeliveredQuantity ?? supply.Quantity);
                    balance.RemoveQuantity(itemId, quantity);
                }
            }

            return;
        }

        if (IsActivity(activity, "RETURN_SUPPLIES"))
        {
            foreach (var supply in supplies.Where(supply => supply.ItemId.HasValue))
            {
                var itemId = supply.ItemId!.Value;
                if (supply.ReturnedLotAllocations is { Count: > 0 })
                {
                    foreach (var lot in supply.ReturnedLotAllocations)
                        balance.RemoveLot(itemId, lot.LotId, lot.QuantityTaken);
                }
                else if (supply.ReturnedReusableUnits is { Count: > 0 })
                {
                    foreach (var unit in supply.ReturnedReusableUnits)
                        balance.RemoveReusableUnit(itemId, unit.ReusableItemId);
                }
                else if (supply.ActualReturnedQuantity.HasValue || subtractPlannedDeliveries)
                {
                    var expectedLotQuantity = supply.ExpectedReturnLotAllocations?.Sum(lot => lot.QuantityTaken) ?? 0;
                    var expectedUnitQuantity = supply.ExpectedReturnUnits?.Count ?? 0;
                    var fallbackQuantity = expectedLotQuantity + expectedUnitQuantity;
                    var quantity = Math.Max(0, supply.ActualReturnedQuantity ?? (fallbackQuantity > 0 ? fallbackQuantity : supply.Quantity));
                    balance.RemoveQuantity(itemId, quantity);
                }
            }
        }
    }

    private static bool IsBefore(MissionActivityModel activity, MissionActivityModel referenceActivity)
    {
        var activityStep = activity.Step ?? int.MaxValue;
        var referenceStep = referenceActivity.Step ?? int.MaxValue;
        if (activityStep != referenceStep)
            return activityStep < referenceStep;

        if (activity.Id == referenceActivity.Id)
            return false;

        return activity.Id < referenceActivity.Id;
    }

    private static bool IsActivity(MissionActivityModel activity, string activityType) =>
        string.Equals(activity.ActivityType, activityType, StringComparison.OrdinalIgnoreCase);
}

internal sealed class MissionSupplyCarriedBalance
{
    private readonly Dictionary<(int ItemId, int? DepotId, int LotId), CarriedLot> _lots = [];
    private readonly Dictionary<int, CarriedReusableUnit> _units = [];
    private readonly Dictionary<int, (string? ItemName, string? Unit)> _itemSummaries = [];

    public void AddLot(int itemId, int? depotId, string? itemName, string? unit, SupplyExecutionLotDto lot)
    {
        RememberItem(itemId, itemName, unit);
        var key = (itemId, depotId, lot.LotId);
        if (_lots.TryGetValue(key, out var existing))
        {
            existing.Lot.QuantityTaken += lot.QuantityTaken;
            existing.Lot.RemainingQuantityAfterExecution = lot.RemainingQuantityAfterExecution;
            return;
        }

        _lots[key] = new CarriedLot(itemId, depotId, itemName, unit, CloneLot(lot));
    }

    public void AddReusableUnit(
        int itemId,
        int? depotId,
        string? itemName,
        string? unitName,
        SupplyExecutionReusableUnitDto unit)
    {
        RememberItem(itemId, string.IsNullOrWhiteSpace(unit.ItemName) ? itemName : unit.ItemName, unitName);
        _units[unit.ReusableItemId] = new CarriedReusableUnit(
            itemId,
            depotId,
            string.IsNullOrWhiteSpace(unit.ItemName) ? itemName : unit.ItemName,
            unitName,
            CloneReusableUnit(unit));
    }

    public void RemoveLot(int itemId, int lotId, int quantity)
    {
        if (quantity <= 0)
            return;

        foreach (var key in _lots.Keys.Where(key => key.ItemId == itemId && key.LotId == lotId).ToList())
        {
            var carried = _lots[key];
            var deduct = Math.Min(carried.Lot.QuantityTaken, quantity);
            carried.Lot.QuantityTaken -= deduct;
            quantity -= deduct;

            if (carried.Lot.QuantityTaken <= 0)
                _lots.Remove(key);

            if (quantity <= 0)
                return;
        }
    }

    public void RemoveReusableUnit(int itemId, int reusableItemId)
    {
        if (_units.TryGetValue(reusableItemId, out var carried) && carried.ItemId == itemId)
            _units.Remove(reusableItemId);
    }

    public void RemoveQuantity(int itemId, int quantity)
    {
        if (quantity <= 0)
            return;

        foreach (var lot in GetLotKeys(itemId))
        {
            if (quantity <= 0)
                return;

            var carried = _lots[lot];
            var deduct = Math.Min(carried.Lot.QuantityTaken, quantity);
            carried.Lot.QuantityTaken -= deduct;
            quantity -= deduct;

            if (carried.Lot.QuantityTaken <= 0)
                _lots.Remove(lot);
        }

        foreach (var reusableItemId in _units.Values
            .Where(unit => unit.ItemId == itemId)
            .OrderBy(unit => unit.Unit.ReusableItemId)
            .Select(unit => unit.Unit.ReusableItemId)
            .ToList())
        {
            if (quantity <= 0)
                return;

            _units.Remove(reusableItemId);
            quantity--;
        }
    }

    public int GetLotQuantity(int itemId, int lotId) =>
        _lots.Values
            .Where(lot => lot.ItemId == itemId && lot.Lot.LotId == lotId)
            .Sum(lot => lot.Lot.QuantityTaken);

    public bool HasReusableUnit(int itemId, int reusableItemId) =>
        _units.TryGetValue(reusableItemId, out var unit) && unit.ItemId == itemId;

    public List<SupplyExecutionLotDto> GetLots(int itemId, int? depotId = null) =>
        _lots.Values
            .Where(lot => lot.ItemId == itemId && (!depotId.HasValue || lot.DepotId == depotId))
            .OrderBy(lot => lot.Lot.ExpiredDate == null ? 1 : 0)
            .ThenBy(lot => lot.Lot.ExpiredDate)
            .ThenBy(lot => lot.Lot.ReceivedDate)
            .ThenBy(lot => lot.Lot.LotId)
            .Select(lot => CloneLot(lot.Lot))
            .ToList();

    public List<SupplyExecutionReusableUnitDto> GetReusableUnits(int itemId, int? depotId = null) =>
        _units.Values
            .Where(unit => unit.ItemId == itemId && (!depotId.HasValue || unit.DepotId == depotId))
            .OrderBy(unit => unit.Unit.ReusableItemId)
            .Select(unit => CloneReusableUnit(unit.Unit))
            .ToList();

    public IEnumerable<int> GetItemIds(int? depotId = null) =>
        _lots.Values
            .Where(lot => !depotId.HasValue || lot.DepotId == depotId)
            .Select(lot => lot.ItemId)
            .Concat(_units.Values
                .Where(unit => !depotId.HasValue || unit.DepotId == depotId)
                .Select(unit => unit.ItemId))
            .Distinct();

    public (string? ItemName, string? Unit) GetItemSummary(int itemId, int? depotId = null)
    {
        var lot = _lots.Values.FirstOrDefault(lot => lot.ItemId == itemId && (!depotId.HasValue || lot.DepotId == depotId));
        if (lot is not null)
            return (lot.ItemName, lot.Unit);

        var reusableUnit = _units.Values.FirstOrDefault(unit => unit.ItemId == itemId && (!depotId.HasValue || unit.DepotId == depotId));
        if (reusableUnit is not null)
            return (reusableUnit.ItemName, reusableUnit.UnitName);

        return _itemSummaries.GetValueOrDefault(itemId);
    }

    private IEnumerable<(int ItemId, int? DepotId, int LotId)> GetLotKeys(int itemId) =>
        _lots.Values
            .Where(lot => lot.ItemId == itemId)
            .OrderBy(lot => lot.Lot.ExpiredDate == null ? 1 : 0)
            .ThenBy(lot => lot.Lot.ExpiredDate)
            .ThenBy(lot => lot.Lot.ReceivedDate)
            .ThenBy(lot => lot.Lot.LotId)
            .Select(lot => (lot.ItemId, lot.DepotId, lot.Lot.LotId))
            .ToList();

    private void RememberItem(int itemId, string? itemName, string? unit)
    {
        if (!_itemSummaries.ContainsKey(itemId) || !string.IsNullOrWhiteSpace(itemName))
            _itemSummaries[itemId] = (itemName, unit);
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
        Condition = unit.Condition,
        Note = unit.Note
    };

    private sealed record CarriedLot(
        int ItemId,
        int? DepotId,
        string? ItemName,
        string? Unit,
        SupplyExecutionLotDto Lot);

    private sealed record CarriedReusableUnit(
        int ItemId,
        int? DepotId,
        string? ItemName,
        string? UnitName,
        SupplyExecutionReusableUnitDto Unit);
}
