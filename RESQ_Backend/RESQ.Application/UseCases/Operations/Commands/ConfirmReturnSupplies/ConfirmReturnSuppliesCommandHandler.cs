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
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

public class ConfirmReturnSuppliesCommandHandler(
    IMissionActivityRepository activityRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IMediator mediator,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<ConfirmReturnSuppliesCommandHandler> logger
) : IRequestHandler<ConfirmReturnSuppliesCommand, ConfirmReturnSuppliesResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ConfirmReturnSuppliesCommandHandler> _logger = logger;

    private const string ReusableItemType = "Reusable";

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ConfirmReturnSuppliesResponse> Handle(
        ConfirmReturnSuppliesCommand request, CancellationToken cancellationToken)
    {
        var consumableItems = request.ConsumableItems ?? [];
        var reusableItems = request.ReusableItems ?? [];

        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy activity với ID {request.ActivityId}.");

        if (!string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ có thể xác nhận trả hàng cho activity loại RETURN_SUPPLIES.");

        if (activity.Status != MissionActivityStatus.PendingConfirmation)
            throw new BadRequestException(
                $"Activity phải ở trạng thái PendingConfirmation để xác nhận. Trạng thái hiện tại: {activity.Status}.");

        if (!activity.DepotId.HasValue)
            throw new BadRequestException("Activity này không có kho liên kết.");

        if (string.IsNullOrWhiteSpace(activity.Items))
            throw new BadRequestException("Activity này không có danh sách hàng hóa.");

        // Validate caller is depot manager of this depot
        var managerDepotIds = await _depotInventoryRepository.GetActiveDepotIdsByManagerAsync(request.ConfirmedBy, cancellationToken);
        if (!managerDepotIds.Contains(activity.DepotId.Value))
            throw new ForbiddenException("Bạn không phải là quản lý kho của depot này. Chỉ quản lý kho mới có quyền xác nhận trả hàng.");

        await MissionSupplyExecutionSnapshotHelper.RebuildExpectedReturnUnitsAsync(
            activity,
            _activityRepository,
            _logger,
            cancellationToken);
        await _unitOfWork.SaveAsync();

        var supplies = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts) ?? [];
        var validItems = supplies
            .Where(s => s.ItemId.HasValue && s.Quantity > 0)
            .ToList();

        if (validItems.Count == 0)
            throw new BadRequestException("Không có hàng hóa hợp lệ trong activity để xác nhận trả.");

        var depotId = activity.DepotId.Value;
        var missionId = activity.MissionId ?? request.MissionId;

        var itemIds = validItems
            .Select(item => item.ItemId!.Value)
            .Distinct()
            .ToList();
        var itemLookup = await _itemModelMetadataRepository.GetByIdsAsync(itemIds, cancellationToken);

        var plannedConsumableQuantities = new Dictionary<int, int>();
        var expectedConsumableLotsByItem = new Dictionary<int, List<SupplyExecutionLotDto>>();
        var expectedConsumableLotIdsByItem = new Dictionary<int, HashSet<int>>();
        var plannedReusableQuantities = new Dictionary<int, int>();
        var expectedReusableUnitsByItem = new Dictionary<int, List<SupplyExecutionReusableUnitDto>>();
        var expectedReusableUnitById = new Dictionary<int, SupplyExecutionReusableUnitDto>();

        foreach (var item in validItems)
        {
            var itemId = item.ItemId!.Value;
            if (!itemLookup.TryGetValue(itemId, out var itemRecord))
                throw new BadRequestException($"Không tìm thấy metadata vật phẩm #{itemId}.");

            if (IsReusableItem(itemRecord))
            {
                var expectedUnits = item.ExpectedReturnUnits?
                    .Where(unit => unit.ReusableItemId > 0)
                    .GroupBy(unit => unit.ReusableItemId)
                    .Select(group => CloneReusableUnit(group.First()))
                    .OrderBy(unit => unit.ReusableItemId)
                    .ToList() ?? [];

                plannedReusableQuantities[itemId] = expectedUnits.Count > 0
                    ? expectedUnits.Count
                    : item.Quantity;

                if (expectedUnits.Count > 0)
                {
                    expectedReusableUnitsByItem[itemId] = expectedUnits;
                    foreach (var expectedUnit in expectedUnits)
                    {
                        expectedReusableUnitById[expectedUnit.ReusableItemId] = expectedUnit;
                    }
                }
            }
            else
            {
                var expectedLots = item.ExpectedReturnLotAllocations?
                    .Where(lot => lot.LotId > 0 && lot.QuantityTaken > 0)
                    .GroupBy(lot => lot.LotId)
                    .Select(group =>
                    {
                        var first = group.First();
                        return new SupplyExecutionLotDto
                        {
                            LotId = group.Key,
                            QuantityTaken = group.Sum(lot => lot.QuantityTaken),
                            ReceivedDate = first.ReceivedDate,
                            ExpiredDate = first.ExpiredDate,
                            RemainingQuantityAfterExecution = first.RemainingQuantityAfterExecution
                        };
                    })
                    .OrderBy(lot => lot.ExpiredDate ?? DateTime.MaxValue)
                    .ThenBy(lot => lot.ReceivedDate ?? DateTime.MaxValue)
                    .ThenBy(lot => lot.LotId)
                    .ToList() ?? [];

                if (expectedLots.Count > 0)
                {
                    expectedConsumableLotsByItem[itemId] = expectedLots;
                    expectedConsumableLotIdsByItem[itemId] = expectedLots
                        .Select(lot => lot.LotId)
                        .ToHashSet();
                    plannedConsumableQuantities[itemId] = expectedLots.Sum(lot => lot.QuantityTaken);
                }
                else
                {
                    plannedConsumableQuantities[itemId] = item.Quantity;
                }
            }
        }

        var hasExpectedReusableSnapshot = expectedReusableUnitById.Count > 0;
        var actualConsumables = new List<(int ItemModelId, int Quantity, DateTime? ExpiredDate, int? SupplyInventoryLotId)>();
        var actualConsumableQuantities = new Dictionary<int, int>();
        var actualConsumableLotQuantities = new Dictionary<(int ItemModelId, int LotId), int>();

        foreach (var consumableItem in consumableItems)
        {
            if (!plannedConsumableQuantities.ContainsKey(consumableItem.ItemModelId))
                throw new BadRequestException($"Item consumable #{consumableItem.ItemModelId} không thuộc kế hoạch RETURN_SUPPLIES này.");

            if (consumableItem.Quantity < 0)
                throw new BadRequestException($"Số lượng consumable trả về cho item #{consumableItem.ItemModelId} không hợp lệ.");

            var requestedLots = (consumableItem.LotAllocations ?? [])
                .Where(lot => lot.QuantityTaken > 0)
                .ToList();
            var requiresLotReturn = expectedConsumableLotsByItem.ContainsKey(consumableItem.ItemModelId);

            if (requestedLots.Count > 0)
            {
                var lotQuantity = requestedLots.Sum(lot => lot.QuantityTaken);
                if (consumableItem.Quantity > 0 && consumableItem.Quantity != lotQuantity)
                    throw new BadRequestException(
                        $"Item consumable #{consumableItem.ItemModelId}: quantity không khớp tổng số lượng lotAllocations.");

                foreach (var requestedLot in requestedLots)
                {
                    if (requestedLot.LotId <= 0)
                        throw new BadRequestException($"Item consumable #{consumableItem.ItemModelId}: lotId không hợp lệ.");

                    if (requiresLotReturn
                        && !expectedConsumableLotIdsByItem[consumableItem.ItemModelId].Contains(requestedLot.LotId))
                    {
                        throw new BadRequestException(
                            $"Lot #{requestedLot.LotId} không nằm trong danh sách expected return của item #{consumableItem.ItemModelId}.");
                    }

                    actualConsumables.Add((
                        consumableItem.ItemModelId,
                        requestedLot.QuantityTaken,
                        requestedLot.ExpiredDate,
                        requestedLot.LotId));
                    actualConsumableQuantities[consumableItem.ItemModelId] =
                        actualConsumableQuantities.GetValueOrDefault(consumableItem.ItemModelId) + requestedLot.QuantityTaken;

                    var lotKey = (consumableItem.ItemModelId, requestedLot.LotId);
                    actualConsumableLotQuantities[lotKey] =
                        actualConsumableLotQuantities.GetValueOrDefault(lotKey) + requestedLot.QuantityTaken;
                }
            }
            else if (consumableItem.Quantity > 0)
            {
                if (requiresLotReturn)
                    throw new BadRequestException(
                        $"Item consumable #{consumableItem.ItemModelId}: mission này yêu cầu xác nhận trả theo từng lot, không cho quantity fallback.");

                actualConsumables.Add((
                    consumableItem.ItemModelId,
                    consumableItem.Quantity,
                    consumableItem.ExpiredDate,
                    null));
                actualConsumableQuantities[consumableItem.ItemModelId] =
                    actualConsumableQuantities.GetValueOrDefault(consumableItem.ItemModelId) + consumableItem.Quantity;
            }
        }

        foreach (var expectedLots in expectedConsumableLotsByItem)
        {
            foreach (var expectedLot in expectedLots.Value)
            {
                var actualLotQuantity = actualConsumableLotQuantities.GetValueOrDefault((expectedLots.Key, expectedLot.LotId));
                if (actualLotQuantity > expectedLot.QuantityTaken)
                {
                    throw new BadRequestException(
                        $"Lot #{expectedLot.LotId} của item #{expectedLots.Key} trả về vượt số lượng expected return ({actualLotQuantity}/{expectedLot.QuantityTaken}).");
                }
            }
        }

        var explicitReusableItems = new List<(int ReusableItemId, string? Condition, string? Note)>();
        var explicitReusableIdsSet = new HashSet<int>();
        var legacyReusableQuantities = new List<(int ItemModelId, int Quantity)>();
        var actualReusableQuantities = new Dictionary<int, int>();

        foreach (var reusableItem in reusableItems)
        {
            if (!plannedReusableQuantities.ContainsKey(reusableItem.ItemModelId))
                throw new BadRequestException($"Item reusable #{reusableItem.ItemModelId} không thuộc kế hoạch RETURN_SUPPLIES này.");

            var explicitUnits = (reusableItem.Units ?? [])
                .Where(unit => unit.ReusableItemId > 0)
                .ToList();

            if (reusableItem.Quantity.HasValue && reusableItem.Quantity.Value < 0)
                throw new BadRequestException($"Số lượng reusable fallback cho item #{reusableItem.ItemModelId} không hợp lệ.");

            if (!hasExpectedReusableSnapshot && explicitUnits.Count > 0 && reusableItem.Quantity.HasValue && reusableItem.Quantity.Value != explicitUnits.Count)
                throw new BadRequestException(
                    $"Item reusable #{reusableItem.ItemModelId}: quantity không khớp số lượng units thực tế được gửi lên.");

            if (hasExpectedReusableSnapshot && explicitUnits.Count == 0 && (reusableItem.Quantity ?? 0) > 0)
                throw new BadRequestException(
                    $"Item reusable #{reusableItem.ItemModelId}: mission này yêu cầu xác nhận trả theo từng unit hoặc serial, không cho quantity fallback.");

            foreach (var unit in explicitUnits)
            {
                if (!explicitReusableIdsSet.Add(unit.ReusableItemId))
                    throw new BadRequestException($"Reusable unit #{unit.ReusableItemId} bị gửi trùng trong payload confirm return.");

                if (hasExpectedReusableSnapshot)
                {
                    if (!expectedReusableUnitById.TryGetValue(unit.ReusableItemId, out var expectedUnit))
                        throw new BadRequestException(
                            $"Reusable unit #{unit.ReusableItemId} không nằm trong danh sách expected return của activity này.");

                    if (expectedUnit.ItemModelId != reusableItem.ItemModelId)
                        throw new BadRequestException(
                            $"Reusable unit #{unit.ReusableItemId} không khớp item model #{reusableItem.ItemModelId}.");
                }

                explicitReusableItems.Add((unit.ReusableItemId, unit.Condition, unit.Note));
            }

            if (!hasExpectedReusableSnapshot && explicitUnits.Count == 0 && (reusableItem.Quantity ?? 0) > 0)
            {
                legacyReusableQuantities.Add((reusableItem.ItemModelId, reusableItem.Quantity!.Value));
            }

            actualReusableQuantities[reusableItem.ItemModelId] =
                actualReusableQuantities.GetValueOrDefault(reusableItem.ItemModelId)
                + explicitUnits.Count
                + (!hasExpectedReusableSnapshot && explicitUnits.Count == 0 ? reusableItem.Quantity ?? 0 : 0);
        }

        var discrepancyDetected = false;

        foreach (var plannedConsumable in plannedConsumableQuantities)
        {
            var actualQuantity = actualConsumableQuantities.GetValueOrDefault(plannedConsumable.Key);
            if (actualQuantity != plannedConsumable.Value)
            {
                discrepancyDetected = true;
                break;
            }
        }

        if (!discrepancyDetected)
        {
            foreach (var plannedReusable in plannedReusableQuantities)
            {
                var actualQuantity = actualReusableQuantities.GetValueOrDefault(plannedReusable.Key);
                if (actualQuantity != plannedReusable.Value)
                {
                    discrepancyDetected = true;
                    break;
                }
            }
        }

        if (discrepancyDetected && string.IsNullOrWhiteSpace(request.DiscrepancyNote))
            throw new BadRequestException("Khi số lượng trả thực tế thiếu hoặc dư so với kế hoạch, phải nhập lý do chênh lệch.");

        MissionSupplyReturnExecutionResult executionResult;
        try
        {
            executionResult = await _depotInventoryRepository.ReceiveMissionReturnByLotAsync(
                depotId,
                missionId,
                request.ActivityId,
                request.ConfirmedBy,
                actualConsumables,
                explicitReusableItems,
                legacyReusableQuantities,
                request.DiscrepancyNote,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        HydrateExpectedReturnData(executionResult, validItems);

        await MissionSupplyExecutionSnapshotHelper.PersistReturnExecutionAsync(
            activity,
            executionResult,
            request.DiscrepancyNote,
            _activityRepository,
            cancellationToken);

        // Dispatch through the full activity lifecycle pipeline so that
        // auto-start of next activity, SOS sync, and team location update all fire.
        await _mediator.Send(
            new UpdateActivityStatusCommand(request.MissionId, request.ActivityId, MissionActivityStatus.Succeed, request.ConfirmedBy),
            cancellationToken);

        _logger.LogInformation(
            "Depot manager confirmed RETURN_SUPPLIES ActivityId={activityId} DepotId={depotId}: {count} item type(s) restocked",
            request.ActivityId, depotId, validItems.Count);

        await _operationalHubService.PushDepotInventoryUpdateAsync(depotId, "ConfirmReturn", cancellationToken);

        return new ConfirmReturnSuppliesResponse
        {
            ActivityId = request.ActivityId,
            MissionId = missionId,
            DepotId = depotId,
            Message = "Xác nhận trả hàng thành công. vật phẩm đã được nhập lại kho.",
            UsedLegacyFallback = executionResult.UsedLegacyFallback,
            DiscrepancyRecorded = discrepancyDetected,
            RestoredItems = executionResult.Items
        };
    }

    private static void HydrateExpectedReturnData(
        MissionSupplyReturnExecutionResult executionResult,
        IEnumerable<SupplyToCollectDto> plannedItems)
    {
        executionResult.Items ??= [];
        var resultLookup = executionResult.Items.ToDictionary(item => item.ItemModelId);

        foreach (var plannedItem in plannedItems)
        {
            if (!plannedItem.ItemId.HasValue)
                continue;

            if (!resultLookup.TryGetValue(plannedItem.ItemId.Value, out var resultItem))
            {
                resultItem = new MissionSupplyReturnExecutionItemDto
                {
                    ItemModelId = plannedItem.ItemId.Value,
                    ItemName = plannedItem.ItemName,
                    Unit = plannedItem.Unit,
                    ActualQuantity = 0
                };
                executionResult.Items.Add(resultItem);
                resultLookup[plannedItem.ItemId.Value] = resultItem;
            }

            var expectedReusableUnits = plannedItem.ExpectedReturnUnits?
                .Select(CloneReusableUnit)
                .OrderBy(unit => unit.ReusableItemId)
                .ToList() ?? [];
            var expectedReturnLots = plannedItem.ExpectedReturnLotAllocations?
                .Select(CloneLot)
                .OrderBy(lot => lot.ExpiredDate ?? DateTime.MaxValue)
                .ThenBy(lot => lot.ReceivedDate ?? DateTime.MaxValue)
                .ThenBy(lot => lot.LotId)
                .ToList() ?? [];

            resultItem.ExpectedQuantity = expectedReturnLots.Count > 0
                ? expectedReturnLots.Sum(lot => lot.QuantityTaken)
                : expectedReusableUnits.Count > 0
                ? expectedReusableUnits.Count
                : plannedItem.Quantity;
            resultItem.ItemName = string.IsNullOrWhiteSpace(resultItem.ItemName)
                ? plannedItem.ItemName
                : resultItem.ItemName;
            resultItem.Unit ??= plannedItem.Unit;
            resultItem.ExpectedReturnLotAllocations = expectedReturnLots;
            resultItem.ExpectedReusableUnits = expectedReusableUnits;
        }

        executionResult.Items = executionResult.Items
            .OrderBy(item => item.ItemModelId)
            .ToList();
    }

    private static bool IsReusableItem(RESQ.Domain.Entities.Logistics.ItemModelRecord itemRecord) =>
        string.Equals(itemRecord.ItemType, ReusableItemType, StringComparison.OrdinalIgnoreCase);

    private static SupplyExecutionReusableUnitDto CloneReusableUnit(SupplyExecutionReusableUnitDto unit) => new()
    {
        ReusableItemId = unit.ReusableItemId,
        ItemModelId = unit.ItemModelId,
        ItemName = unit.ItemName,
        SerialNumber = unit.SerialNumber,
        Condition = unit.Condition
    };

    private static SupplyExecutionLotDto CloneLot(SupplyExecutionLotDto lot) => new()
    {
        LotId = lot.LotId,
        QuantityTaken = lot.QuantityTaken,
        ReceivedDate = lot.ReceivedDate,
        ExpiredDate = lot.ExpiredDate,
        RemainingQuantityAfterExecution = lot.RemainingQuantityAfterExecution
    };
}
