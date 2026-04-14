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
    IUnitOfWork unitOfWork,
    ILogger<ConfirmReturnSuppliesCommandHandler> logger
) : IRequestHandler<ConfirmReturnSuppliesCommand, ConfirmReturnSuppliesResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IMediator _mediator = mediator;
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
            ?? throw new NotFoundException($"Không tìm th?y activity v?i ID {request.ActivityId}.");

        if (!string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Ch? có th? xác nh?n tr? hàng cho activity lo?i RETURN_SUPPLIES.");

        if (activity.Status != MissionActivityStatus.PendingConfirmation)
            throw new BadRequestException(
                $"Activity ph?i ? tr?ng thái PendingConfirmation d? xác nh?n. Tr?ng thái hi?n t?i: {activity.Status}.");

        if (!activity.DepotId.HasValue)
            throw new BadRequestException("Activity này không có kho liên k?t.");

        if (string.IsNullOrWhiteSpace(activity.Items))
            throw new BadRequestException("Activity này không có danh sách hàng hóa.");

        // Validate caller is depot manager of this depot
        var managerDepotIds = await _depotInventoryRepository.GetActiveDepotIdsByManagerAsync(request.ConfirmedBy, cancellationToken);
        if (!managerDepotIds.Contains(activity.DepotId.Value))
            throw new ForbiddenException("B?n không ph?i là qu?n lý kho c?a depot này. Ch? qu?n lý kho m?i có quy?n xác nh?n tr? hàng.");

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
            throw new BadRequestException("Không có hàng hóa h?p l? trong activity d? xác nh?n tr?.");

        var depotId = activity.DepotId.Value;
        var missionId = activity.MissionId ?? request.MissionId;

        var itemIds = validItems
            .Select(item => item.ItemId!.Value)
            .Distinct()
            .ToList();
        var itemLookup = await _itemModelMetadataRepository.GetByIdsAsync(itemIds, cancellationToken);

        var plannedConsumableQuantities = new Dictionary<int, int>();
        var plannedReusableQuantities = new Dictionary<int, int>();
        var expectedReusableUnitsByItem = new Dictionary<int, List<SupplyExecutionReusableUnitDto>>();
        var expectedReusableUnitById = new Dictionary<int, SupplyExecutionReusableUnitDto>();

        foreach (var item in validItems)
        {
            var itemId = item.ItemId!.Value;
            if (!itemLookup.TryGetValue(itemId, out var itemRecord))
                throw new BadRequestException($"Không tìm th?y metadata v?t ph?m #{itemId}.");

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
                plannedConsumableQuantities[itemId] = item.Quantity;
            }
        }

        var hasExpectedReusableSnapshot = expectedReusableUnitById.Count > 0;
        var actualConsumables = consumableItems
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.ItemModelId)
            .Select(group => new ActualReturnedConsumableItemDto
            {
                ItemModelId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();

        foreach (var actualConsumable in actualConsumables)
        {
            if (!plannedConsumableQuantities.ContainsKey(actualConsumable.ItemModelId))
                throw new BadRequestException($"Item consumable #{actualConsumable.ItemModelId} không thu?c k? ho?ch RETURN_SUPPLIES này.");
        }

        var explicitReusableItems = new List<(int ReusableItemId, string? Condition, string? Note)>();
        var explicitReusableIdsSet = new HashSet<int>();
        var legacyReusableQuantities = new List<(int ItemModelId, int Quantity)>();
        var actualReusableQuantities = new Dictionary<int, int>();

        foreach (var reusableItem in reusableItems)
        {
            if (!plannedReusableQuantities.ContainsKey(reusableItem.ItemModelId))
                throw new BadRequestException($"Item reusable #{reusableItem.ItemModelId} không thu?c k? ho?ch RETURN_SUPPLIES này.");

            var explicitUnits = (reusableItem.Units ?? [])
                .Where(unit => unit.ReusableItemId > 0)
                .ToList();

            if (reusableItem.Quantity.HasValue && reusableItem.Quantity.Value < 0)
                throw new BadRequestException($"S? lu?ng reusable fallback cho item #{reusableItem.ItemModelId} không h?p l?.");

            if (!hasExpectedReusableSnapshot && explicitUnits.Count > 0 && reusableItem.Quantity.HasValue && reusableItem.Quantity.Value != explicitUnits.Count)
                throw new BadRequestException(
                    $"Item reusable #{reusableItem.ItemModelId}: quantity không kh?p s? lu?ng units th?c t? du?c g?i lên.");

            if (hasExpectedReusableSnapshot && explicitUnits.Count == 0 && (reusableItem.Quantity ?? 0) > 0)
                throw new BadRequestException(
                    $"Item reusable #{reusableItem.ItemModelId}: mission này yêu c?u xác nh?n tr? theo t?ng unit ho?c serial, không cho quantity fallback.");

            foreach (var unit in explicitUnits)
            {
                if (!explicitReusableIdsSet.Add(unit.ReusableItemId))
                    throw new BadRequestException($"Reusable unit #{unit.ReusableItemId} b? g?i trùng trong payload confirm return.");

                if (hasExpectedReusableSnapshot)
                {
                    if (!expectedReusableUnitById.TryGetValue(unit.ReusableItemId, out var expectedUnit))
                        throw new BadRequestException(
                            $"Reusable unit #{unit.ReusableItemId} không n?m trong danh sách expected return c?a activity này.");

                    if (expectedUnit.ItemModelId != reusableItem.ItemModelId)
                        throw new BadRequestException(
                            $"Reusable unit #{unit.ReusableItemId} không kh?p item model #{reusableItem.ItemModelId}.");
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
            var actualQuantity = actualConsumables
                .FirstOrDefault(item => item.ItemModelId == plannedConsumable.Key)
                ?.Quantity ?? 0;
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
            throw new BadRequestException("Khi s? lu?ng tr? th?c t? thi?u ho?c du so v?i k? ho?ch, ph?i nh?p lý do chênh l?ch.");

        MissionSupplyReturnExecutionResult executionResult;
        try
        {
            executionResult = await _depotInventoryRepository.ReceiveMissionReturnAsync(
                depotId,
                missionId,
                request.ActivityId,
                request.ConfirmedBy,
                actualConsumables.Select(item => (item.ItemModelId, item.Quantity)).ToList(),
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

        return new ConfirmReturnSuppliesResponse
        {
            ActivityId = request.ActivityId,
            MissionId = missionId,
            DepotId = depotId,
            Message = "Xác nh?n tr? hàng thành công. v?t ph?m dã du?c nh?p l?i kho.",
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

            resultItem.ExpectedQuantity = expectedReusableUnits.Count > 0
                ? expectedReusableUnits.Count
                : plannedItem.Quantity;
            resultItem.ItemName = string.IsNullOrWhiteSpace(resultItem.ItemName)
                ? plannedItem.ItemName
                : resultItem.ItemName;
            resultItem.Unit ??= plannedItem.Unit;
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
}
