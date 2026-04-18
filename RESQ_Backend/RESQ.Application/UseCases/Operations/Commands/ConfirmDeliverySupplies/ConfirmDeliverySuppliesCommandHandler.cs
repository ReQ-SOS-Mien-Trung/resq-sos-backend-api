using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesCommandHandler(
    IMissionActivityRepository activityRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IMissionTeamReportRepository missionTeamReportRepository,
    IMediator mediator,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<ConfirmDeliverySuppliesCommandHandler> logger
) : IRequestHandler<ConfirmDeliverySuppliesCommand, ConfirmDeliverySuppliesResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IMissionTeamReportRepository _missionTeamReportRepository = missionTeamReportRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ConfirmDeliverySuppliesCommandHandler> _logger = logger;

    private const string ReusableItemType = "Reusable";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ConfirmDeliverySuppliesResponse> Handle(
        ConfirmDeliverySuppliesCommand request,
        CancellationToken cancellationToken)
    {
        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy activity với ID {request.ActivityId}.");

        if (activity.MissionId != request.MissionId)
            throw new BadRequestException("Activity này không thuộc mission được chỉ định.");

        if (!string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ có thể xác nhận giao hàng cho activity loại DELIVER_SUPPLIES.");

        if (activity.Status != MissionActivityStatus.OnGoing)
        {
            throw new BadRequestException(
                $"Activity phải ở trạng thái OnGoing để xác nhận giao hàng. Trạng thái hiện tại: {activity.Status}.");
        }

        if (string.IsNullOrWhiteSpace(activity.Items))
            throw new BadRequestException("Activity này không có danh sách hàng hóa.");

        var supplies = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, JsonOpts) ?? [];
        var validSupplies = supplies.Where(s => s.ItemId.HasValue).ToList();
        var supplyLookup = validSupplies.ToDictionary(s => s.ItemId!.Value);

        foreach (var deliveredItem in request.ActualDeliveredItems)
        {
            if (!supplyLookup.ContainsKey(deliveredItem.ItemId))
                throw new BadRequestException($"ItemId {deliveredItem.ItemId} không tồn tại trong activity này.");
        }

        var missionActivities = (await _activityRepository
            .GetByMissionIdAsync(activity.MissionId ?? request.MissionId, cancellationToken))
            .ToList();
        ReplaceActivitySnapshot(missionActivities, activity);

        var carriedBalance = MissionSupplyCarriedBalanceHelper.CalculateBeforeActivity(
            missionActivities,
            activity,
            subtractPlannedDeliveries: true);

        var deliveredLookup = request.ActualDeliveredItems
            .GroupBy(item => item.ItemId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var supply in validSupplies)
        {
            var itemId = supply.ItemId!.Value;
            if (!deliveredLookup.TryGetValue(itemId, out var deliveredItem))
                continue;

            ApplyDeliveredItemSnapshot(supply, deliveredItem, carriedBalance);
        }

        activity.Items = JsonSerializer.Serialize(supplies);
        await _activityRepository.UpdateAsync(activity, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _mediator.Send(
            new UpdateActivityStatusCommand(
                request.MissionId,
                request.ActivityId,
                MissionActivityStatus.Succeed,
                request.ConfirmedBy),
            cancellationToken);

        ReplaceActivitySnapshot(missionActivities, activity);
        var surplusReturnActivityId = await RefreshReturnActivityAsync(
            activity,
            missionActivities,
            carriedBalance,
            cancellationToken);
        if (surplusReturnActivityId.HasValue)
            await _unitOfWork.SaveAsync();

        var normalizedDeliveryNote = request.DeliveryNote?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDeliveryNote))
            await SaveDeliveryNoteToDraftReportAsync(activity, normalizedDeliveryNote, cancellationToken);

        _logger.LogInformation(
            "Team confirmed DELIVER_SUPPLIES ActivityId={ActivityId} MissionId={MissionId}: {ItemCount} item type(s) delivered. ReturnActivityId={ReturnActivityId}",
            request.ActivityId,
            request.MissionId,
            validSupplies.Count,
            surplusReturnActivityId?.ToString() ?? "none");

        var resultItems = validSupplies
            .Where(s => s.ItemId.HasValue)
            .Select(s =>
            {
                var deliveredLots = s.DeliveredLotAllocations ?? [];
                var deliveredUnits = s.DeliveredReusableUnits ?? [];
                var actualQty = s.ActualDeliveredQuantity
                    ?? deliveredLots.Sum(lot => lot.QuantityTaken)
                    + deliveredUnits.Count;

                return new DeliveryItemResultDto
                {
                    ItemId = s.ItemId!.Value,
                    ItemName = s.ItemName,
                    Unit = s.Unit,
                    PlannedQuantity = s.Quantity,
                    ActualDeliveredQuantity = actualQty,
                    SurplusQuantity = Math.Max(0, s.Quantity - actualQty),
                    DeliveredLotAllocations = deliveredLots.Select(CloneLot).ToList(),
                    DeliveredReusableUnits = deliveredUnits.Select(CloneReusableUnit).ToList()
                };
            })
            .ToList();

        if (activity.DepotId.HasValue)
        {
            await _operationalHubService.PushDepotInventoryUpdateAsync(
                activity.DepotId.Value,
                "ConfirmDelivery",
                cancellationToken);
        }

        return new ConfirmDeliverySuppliesResponse
        {
            ActivityId = request.ActivityId,
            MissionId = request.MissionId,
            Status = MissionActivityStatus.Succeed.ToString(),
            Message = surplusReturnActivityId.HasValue
                ? $"Xác nhận giao hàng thành công. Đã cập nhật activity trả hàng #{surplusReturnActivityId} theo số hàng còn mang theo."
                : "Xác nhận giao hàng thành công.",
            SurplusReturnActivityId = surplusReturnActivityId,
            DeliveredItems = resultItems
        };
    }

    private static void ApplyDeliveredItemSnapshot(
        SupplyToCollectDto supply,
        ActualDeliveredItemDto deliveredItem,
        MissionSupplyCarriedBalance carriedBalance)
    {
        var itemId = supply.ItemId!.Value;
        var providedLots = (deliveredItem.LotAllocations ?? [])
            .Where(lot => lot.QuantityTaken > 0)
            .ToList();
        var providedUnits = (deliveredItem.ReusableUnits ?? [])
            .Where(unit => unit.ReusableItemId > 0)
            .ToList();

        if (providedLots.Count > 0 && providedUnits.Count > 0)
            throw new BadRequestException($"Item #{itemId}: không được vừa giao theo lot vừa giao theo reusable unit.");

        if (providedLots.Count > 0)
        {
            var actualQuantity = providedLots.Sum(lot => lot.QuantityTaken);
            EnsureActualQuantityMatches(itemId, deliveredItem.ActualQuantity, actualQuantity);
            EnsureWithinPlannedQuantity(itemId, supply.Quantity, actualQuantity);

            var deliveredLots = new List<SupplyExecutionLotDto>();
            foreach (var lot in providedLots)
            {
                var availableQuantity = carriedBalance.GetLotQuantity(itemId, lot.LotId);
                if (availableQuantity < lot.QuantityTaken)
                {
                    throw new BadRequestException(
                        $"Item #{itemId}, lot #{lot.LotId}: số lượng giao {lot.QuantityTaken} vượt quá số đang mang theo {availableQuantity}.");
                }

                var sourceLot = carriedBalance.GetLots(itemId)
                    .FirstOrDefault(x => x.LotId == lot.LotId);
                deliveredLots.Add(new SupplyExecutionLotDto
                {
                    LotId = lot.LotId,
                    QuantityTaken = lot.QuantityTaken,
                    ReceivedDate = sourceLot?.ReceivedDate ?? lot.ReceivedDate,
                    ExpiredDate = sourceLot?.ExpiredDate ?? lot.ExpiredDate,
                    RemainingQuantityAfterExecution = Math.Max(0, availableQuantity - lot.QuantityTaken)
                });
            }

            supply.DeliveredLotAllocations = deliveredLots;
            supply.DeliveredReusableUnits = null;
            supply.ActualDeliveredQuantity = actualQuantity;
            return;
        }

        if (providedUnits.Count > 0)
        {
            var actualQuantity = providedUnits.Count;
            EnsureActualQuantityMatches(itemId, deliveredItem.ActualQuantity, actualQuantity);
            EnsureWithinPlannedQuantity(itemId, supply.Quantity, actualQuantity);

            var deliveredUnits = new List<SupplyExecutionReusableUnitDto>();
            foreach (var unit in providedUnits)
            {
                var sourceUnit = carriedBalance.GetReusableUnits(itemId)
                    .FirstOrDefault(x => x.ReusableItemId == unit.ReusableItemId);
                if (sourceUnit is null)
                {
                    throw new BadRequestException(
                        $"Reusable unit #{unit.ReusableItemId} không nằm trong danh sách team đang mang theo cho item #{itemId}.");
                }

                deliveredUnits.Add(new SupplyExecutionReusableUnitDto
                {
                    ReusableItemId = sourceUnit.ReusableItemId,
                    ItemModelId = sourceUnit.ItemModelId,
                    ItemName = sourceUnit.ItemName,
                    SerialNumber = sourceUnit.SerialNumber,
                    Condition = unit.Condition ?? sourceUnit.Condition,
                    Note = unit.Note ?? sourceUnit.Note
                });
            }

            supply.DeliveredReusableUnits = deliveredUnits;
            supply.DeliveredLotAllocations = null;
            supply.ActualDeliveredQuantity = actualQuantity;
            return;
        }

        var hasDetailedCarrySnapshot =
            carriedBalance.GetLots(itemId).Count > 0
            || carriedBalance.GetReusableUnits(itemId).Count > 0;
        if (hasDetailedCarrySnapshot && deliveredItem.ActualQuantity > 0)
        {
            throw new BadRequestException(
                $"Item #{itemId}: mission này yêu cầu xác nhận delivery theo lot hoặc reusable unit, không chỉ gửi quantity.");
        }

        EnsureWithinPlannedQuantity(itemId, supply.Quantity, deliveredItem.ActualQuantity);
        supply.DeliveredLotAllocations = null;
        supply.DeliveredReusableUnits = null;
        supply.ActualDeliveredQuantity = deliveredItem.ActualQuantity;
    }

    private async Task<int?> RefreshReturnActivityAsync(
        MissionActivityModel deliverActivity,
        List<MissionActivityModel> missionActivities,
        MissionSupplyCarriedBalance carriedBalanceBeforeDelivery,
        CancellationToken cancellationToken)
    {
        if (!deliverActivity.MissionId.HasValue || !deliverActivity.MissionTeamId.HasValue)
            return null;

        var existingReturnActivity = missionActivities
            .Where(activity => activity.Id != deliverActivity.Id
                && activity.MissionTeamId == deliverActivity.MissionTeamId
                && activity.DepotId == deliverActivity.DepotId
                && string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                && activity.Status is MissionActivityStatus.Planned
                    or MissionActivityStatus.OnGoing
                    or MissionActivityStatus.PendingConfirmation)
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .FirstOrDefault();

        if (existingReturnActivity is not null)
        {
            var originalDescription = existingReturnActivity.Description;
            var currentSupplies = MissionSupplyCarriedBalanceHelper.ParseSupplies(existingReturnActivity.Items);
            var expectedSupplies = MissionSupplyCarriedBalanceHelper.BuildExpectedReturnSupplies(
                existingReturnActivity,
                missionActivities,
                currentSupplies);
            var addedLegacyShortage = await AppendLegacyConsumableShortagesAsync(
                deliverActivity,
                expectedSupplies,
                carriedBalanceBeforeDelivery,
                cancellationToken);

            if (expectedSupplies.Count == 0
                && !addedLegacyShortage
                && !HasAnyCarryBeforeDelivery(deliverActivity, carriedBalanceBeforeDelivery))
            {
                return null;
            }

            var updatedItemsJson = expectedSupplies.Count == 0
                ? null
                : JsonSerializer.Serialize(expectedSupplies);
            if (addedLegacyShortage)
            {
                existingReturnActivity.Description = AppendReturnShortageDescription(
                    existingReturnActivity.Description,
                    deliverActivity.Id);
            }

            if (string.Equals(existingReturnActivity.Items, updatedItemsJson, StringComparison.Ordinal)
                && string.Equals(originalDescription, existingReturnActivity.Description, StringComparison.Ordinal))
            {
                return null;
            }

            existingReturnActivity.Items = expectedSupplies.Count == 0
                ? null
                : updatedItemsJson;
            await _activityRepository.UpdateAsync(existingReturnActivity, cancellationToken);
            return existingReturnActivity.Id;
        }

        var endBalance = MissionSupplyCarriedBalanceHelper.CalculateAtEndForTeam(
            missionActivities,
            deliverActivity.MissionTeamId,
            subtractPlannedDeliveries: true);
        var returnSupplies = MissionSupplyCarriedBalanceHelper.BuildSuppliesFromBalance(
            endBalance,
            deliverActivity.DepotId);
        await AppendLegacyConsumableShortagesAsync(
            deliverActivity,
            returnSupplies,
            carriedBalanceBeforeDelivery,
            cancellationToken);

        if (returnSupplies.Count == 0)
            return null;

        var insertionStep = MissionReturnAssemblyPointStepHelper.ReserveStepBeforeReturnAssemblyPoint(
            missionActivities,
            deliverActivity.MissionTeamId,
            out var shiftedActivities);

        foreach (var shiftedActivity in shiftedActivities)
            await _activityRepository.UpdateAsync(shiftedActivity, cancellationToken);

        var returnActivity = new MissionActivityModel
        {
            MissionId = deliverActivity.MissionId,
            Step = insertionStep,
            ActivityType = "RETURN_SUPPLIES",
            Description = $"Trả vật phẩm còn lại về kho {deliverActivity.DepotName} sau delivery activity #{deliverActivity.Id}",
            Priority = deliverActivity.Priority,
            EstimatedTime = deliverActivity.EstimatedTime,
            SosRequestId = deliverActivity.SosRequestId,
            DepotId = deliverActivity.DepotId,
            DepotName = deliverActivity.DepotName,
            DepotAddress = deliverActivity.DepotAddress,
            Items = JsonSerializer.Serialize(returnSupplies),
            Status = MissionActivityStatus.Planned
        };

        var returnActivityId = await _activityRepository.AddAsync(returnActivity, cancellationToken);
        if (deliverActivity.MissionTeamId.HasValue)
            await _activityRepository.AssignTeamAsync(returnActivityId, deliverActivity.MissionTeamId.Value, cancellationToken);

        return returnActivityId;
    }

    private static string AppendReturnShortageDescription(string? description, int deliveryActivityId)
    {
        var note = $"Bổ sung vật phẩm giao thiếu từ activity #{deliveryActivityId}.";
        if (!string.IsNullOrWhiteSpace(description)
            && description.Contains(note, StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        return string.IsNullOrWhiteSpace(description)
            ? note
            : $"{description.TrimEnd()}{Environment.NewLine}{note}";
    }

    private async Task<bool> AppendLegacyConsumableShortagesAsync(
        MissionActivityModel deliverActivity,
        List<SupplyToCollectDto> returnSupplies,
        MissionSupplyCarriedBalance carriedBalanceBeforeDelivery,
        CancellationToken cancellationToken)
    {
        var deliveredSupplies = MissionSupplyCarriedBalanceHelper.ParseSupplies(deliverActivity.Items)
            .Where(supply => supply.ItemId.HasValue)
            .ToList();
        var shortageCandidates = deliveredSupplies
            .Where(supply => !HasDetailedCarryForItem(supply.ItemId!.Value, carriedBalanceBeforeDelivery))
            .Where(supply => Math.Max(0, supply.Quantity - (supply.ActualDeliveredQuantity ?? supply.Quantity)) > 0)
            .ToList();

        if (shortageCandidates.Count == 0)
            return false;

        var itemIds = shortageCandidates
            .Select(supply => supply.ItemId!.Value)
            .Distinct()
            .ToList();
        var itemLookup = await _itemModelMetadataRepository.GetByIdsAsync(itemIds, cancellationToken);
        var changed = false;

        foreach (var supply in shortageCandidates)
        {
            var itemId = supply.ItemId!.Value;
            if (!itemLookup.TryGetValue(itemId, out var itemRecord)
                || string.Equals(itemRecord.ItemType, ReusableItemType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var shortageQuantity = Math.Max(0, supply.Quantity - (supply.ActualDeliveredQuantity ?? supply.Quantity));
            if (shortageQuantity <= 0)
                continue;

            var existing = returnSupplies.FirstOrDefault(item => item.ItemId == itemId);
            if (existing is not null)
            {
                existing.Quantity += shortageQuantity;
                changed = true;
                continue;
            }

            returnSupplies.Add(new SupplyToCollectDto
            {
                ItemId = itemId,
                ItemName = supply.ItemName,
                ImageUrl = supply.ImageUrl,
                Quantity = shortageQuantity,
                Unit = supply.Unit
            });
            changed = true;
        }

        return changed;
    }

    private static bool HasAnyCarryBeforeDelivery(
        MissionActivityModel deliverActivity,
        MissionSupplyCarriedBalance carriedBalanceBeforeDelivery)
    {
        var deliveredSupplies = MissionSupplyCarriedBalanceHelper.ParseSupplies(deliverActivity.Items);
        return deliveredSupplies
            .Where(supply => supply.ItemId.HasValue)
            .Any(supply => HasDetailedCarryForItem(supply.ItemId!.Value, carriedBalanceBeforeDelivery));
    }

    private static bool HasDetailedCarryForItem(
        int itemId,
        MissionSupplyCarriedBalance carriedBalanceBeforeDelivery) =>
        carriedBalanceBeforeDelivery.GetLots(itemId).Count > 0
        || carriedBalanceBeforeDelivery.GetReusableUnits(itemId).Count > 0;

    private async Task SaveDeliveryNoteToDraftReportAsync(
        MissionActivityModel activity,
        string deliveryNote,
        CancellationToken cancellationToken)
    {
        if (!activity.MissionTeamId.HasValue)
            return;

        var teamId = activity.MissionTeamId.Value;
        var draft = await _missionTeamReportRepository.GetByMissionTeamIdAsync(teamId, cancellationToken);
        draft ??= new MissionTeamReportModel { MissionTeamId = teamId };

        var activityReport = draft.ActivityReports.FirstOrDefault(r => r.MissionActivityId == activity.Id);
        if (activityReport is null)
        {
            activityReport = new MissionActivityReportModel
            {
                MissionActivityId = activity.Id
            };
            draft.ActivityReports.Add(activityReport);
        }

        activityReport.ActivityType ??= activity.ActivityType;
        activityReport.ExecutionStatus = MissionActivityStatus.Succeed.ToString();

        var existingLines = (activityReport.Summary ?? string.Empty)
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (existingLines.Any(line => string.Equals(line, deliveryNote, StringComparison.OrdinalIgnoreCase)))
            return;

        activityReport.Summary = string.IsNullOrWhiteSpace(activityReport.Summary)
            ? deliveryNote
            : $"{activityReport.Summary.TrimEnd()}{Environment.NewLine}{deliveryNote}";

        await _missionTeamReportRepository.UpsertDraftAsync(draft, cancellationToken);
    }

    private static void ReplaceActivitySnapshot(List<MissionActivityModel> activities, MissionActivityModel activity)
    {
        var index = activities.FindIndex(item => item.Id == activity.Id);
        if (index >= 0)
        {
            activities[index] = activity;
            return;
        }

        activities.Add(activity);
    }

    private static void EnsureActualQuantityMatches(int itemId, int requestQuantity, int detailedQuantity)
    {
        if (requestQuantity > 0 && requestQuantity != detailedQuantity)
            throw new BadRequestException(
                $"Item #{itemId}: ActualQuantity ({requestQuantity}) không khớp tổng số lượng lot/unit ({detailedQuantity}).");
    }

    private static void EnsureWithinPlannedQuantity(int itemId, int plannedQuantity, int actualQuantity)
    {
        if (actualQuantity < 0)
            throw new BadRequestException($"Item #{itemId}: ActualQuantity phải >= 0.");

        if (actualQuantity > plannedQuantity)
            throw new BadRequestException(
                $"Item #{itemId}: số lượng giao thực tế {actualQuantity} vượt quá số lượng kế hoạch {plannedQuantity}.");
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
}
