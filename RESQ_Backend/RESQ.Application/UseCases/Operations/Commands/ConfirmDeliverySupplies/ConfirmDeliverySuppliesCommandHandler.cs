using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesCommandHandler(
    IMissionActivityRepository activityRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<ConfirmDeliverySuppliesCommandHandler> logger
) : IRequestHandler<ConfirmDeliverySuppliesCommand, ConfirmDeliverySuppliesResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ConfirmDeliverySuppliesCommandHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ConfirmDeliverySuppliesResponse> Handle(
        ConfirmDeliverySuppliesCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch and validate activity
        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy activity với ID {request.ActivityId}.");

        if (activity.MissionId != request.MissionId)
            throw new BadRequestException("Activity này không thuộc mission được chỉ định.");

        if (!string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ có thể xác nhận giao hàng cho activity loại DELIVER_SUPPLIES.");

        if (activity.Status != MissionActivityStatus.OnGoing)
            throw new BadRequestException(
                $"Activity phải ở trạng thái OnGoing để xác nhận giao hàng. Trạng thái hiện tại: {activity.Status}.");

        if (string.IsNullOrWhiteSpace(activity.Items))
            throw new BadRequestException("Activity này không có danh sách hàng hóa.");

        // 2. Deserialize current items JSON
        var supplies = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts) ?? [];
        var validSupplies = supplies.Where(s => s.ItemId.HasValue).ToList();

        // 3. Validate all submitted ItemIds exist in the activity
        var supplyLookup = validSupplies.ToDictionary(s => s.ItemId!.Value);

        foreach (var deliveredItem in request.ActualDeliveredItems)
        {
            if (!supplyLookup.ContainsKey(deliveredItem.ItemId))
                throw new BadRequestException(
                    $"ItemId {deliveredItem.ItemId} không tồn tại trong danh sách vật tư của activity này.");
        }

        // 4. Apply actual delivered quantities into Items JSON then persist
        var deliveredLookup = request.ActualDeliveredItems.ToDictionary(d => d.ItemId);
        foreach (var supply in supplies)
        {
            if (supply.ItemId.HasValue && deliveredLookup.TryGetValue(supply.ItemId.Value, out var deliveredItem))
                supply.ActualDeliveredQuantity = deliveredItem.ActualQuantity;
        }

        activity.Items = JsonSerializer.Serialize(supplies);
        await _activityRepository.UpdateAsync(activity, cancellationToken);
        // Save Items changes before dispatching status command so the inner handler
        // fetches a fresh entity with ActualDeliveredQuantity already persisted.
        await _unitOfWork.SaveAsync();

        // 5. Dispatch UpdateActivityStatusCommand(Succeed) - reuses all side-effects:
        //    team location update, SOS sync, auto-chain next activity.
        await _mediator.Send(
            new UpdateActivityStatusCommand(request.MissionId, request.ActivityId, MissionActivityStatus.Succeed, request.ConfirmedBy),
            cancellationToken);

        // 6. Upsert RETURN_SUPPLIES for any consumable surplus (planned > actual delivered).
        // Reusable units are already handled by the mission's final RETURN_SUPPLIES activity.
        int? surplusReturnActivityId = null;
        var surplusCandidates = validSupplies
            .Where(s => s.ItemId.HasValue
                && deliveredLookup.TryGetValue(s.ItemId.Value, out var d)
                && d.ActualQuantity < s.Quantity)
            .ToList();

        var surplusItems = new List<SupplyToCollectDto>();
        if (surplusCandidates.Count > 0)
        {
            var itemMetadata = await _itemModelMetadataRepository.GetByIdsAsync(
                surplusCandidates.Select(s => s.ItemId!.Value).Distinct().ToList(),
                cancellationToken);

            foreach (var supply in surplusCandidates)
            {
                var itemId = supply.ItemId!.Value;
                if (!itemMetadata.TryGetValue(itemId, out var metadata))
                    throw new BadRequestException($"Không tìm thấy metadata vật tư #{itemId}.");

                if (string.Equals(metadata.ItemType, "Reusable", StringComparison.OrdinalIgnoreCase))
                    continue;

                surplusItems.Add(new SupplyToCollectDto
                {
                    ItemId = supply.ItemId,
                    ItemName = supply.ItemName,
                    ImageUrl = supply.ImageUrl,
                    Unit = supply.Unit,
                    Quantity = supply.Quantity - deliveredLookup[itemId].ActualQuantity
                });
            }
        }

        if (surplusItems.Count > 0 && activity.DepotId.HasValue)
        {
            surplusReturnActivityId = await UpsertSurplusReturnActivityAsync(
                activity, surplusItems, request.ConfirmedBy, cancellationToken);
            await _unitOfWork.SaveAsync();
        }

        _logger.LogInformation(
            "Team confirmed DELIVER_SUPPLIES ActivityId={activityId} MissionId={missionId}: {itemCount} item type(s) delivered. SurplusReturnActivityId={surplusReturn}",
            request.ActivityId, request.MissionId, validSupplies.Count, surplusReturnActivityId?.ToString() ?? "none");

        // 7. Build response
        var resultItems = validSupplies
            .Where(s => s.ItemId.HasValue)
            .Select(s =>
            {
                var actualQty = deliveredLookup.TryGetValue(s.ItemId!.Value, out var d) ? d.ActualQuantity : s.Quantity;
                return new DeliveryItemResultDto
                {
                    ItemId = s.ItemId!.Value,
                    ItemName = s.ItemName,
                    Unit = s.Unit,
                    PlannedQuantity = s.Quantity,
                    ActualDeliveredQuantity = actualQty,
                    SurplusQuantity = Math.Max(0, s.Quantity - actualQty)
                };
            })
            .ToList();

        return new ConfirmDeliverySuppliesResponse
        {
            ActivityId = request.ActivityId,
            MissionId = request.MissionId,
            Status = MissionActivityStatus.Succeed.ToString(),
            Message = surplusReturnActivityId.HasValue
                ? $"Xác nhận giao hàng thành công. Đã cập nhật activity trả hàng #{surplusReturnActivityId} cho vật tư giao thiếu."
                : "Xác nhận giao hàng thành công.",
            SurplusReturnActivityId = surplusReturnActivityId,
            DeliveredItems = resultItems
        };
    }

    private async Task<int> UpsertSurplusReturnActivityAsync(
        MissionActivityModel deliverActivity,
        List<SupplyToCollectDto> surplusItems,
        Guid decidedBy,
        CancellationToken cancellationToken)
    {
        var missionId = deliverActivity.MissionId ?? 0;
        var existingActivities = (await _activityRepository.GetByMissionIdAsync(missionId, cancellationToken)).ToList();
        var existingReturnActivity = existingActivities
            .Where(activity => activity.Id != deliverActivity.Id
                && activity.MissionTeamId == deliverActivity.MissionTeamId
                && activity.DepotId == deliverActivity.DepotId
                && string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                && activity.Status is MissionActivityStatus.Planned
                    or MissionActivityStatus.OnGoing
                    or MissionActivityStatus.PendingConfirmation
                && (!deliverActivity.Step.HasValue
                    || !activity.Step.HasValue
                    || activity.Step.Value > deliverActivity.Step.Value))
            .OrderBy(activity => activity.Step ?? int.MaxValue)
            .ThenBy(activity => activity.Id)
            .FirstOrDefault();

        if (existingReturnActivity is not null)
        {
            MergeSurplusItems(existingReturnActivity, surplusItems, deliverActivity.Id);
            await _activityRepository.UpdateAsync(existingReturnActivity, cancellationToken);

            _logger.LogInformation(
                "Merged surplus from DELIVER_SUPPLIES ActivityId={deliverActivityId} into existing RETURN_SUPPLIES ActivityId={returnActivityId} MissionId={missionId}",
                deliverActivity.Id, existingReturnActivity.Id, missionId);

            return existingReturnActivity.Id;
        }

        var insertionStep = MissionReturnAssemblyPointStepHelper.ReserveStepBeforeReturnAssemblyPoint(
            existingActivities,
            deliverActivity.MissionTeamId,
            out var shiftedActivities);

        foreach (var shiftedActivity in shiftedActivities)
            await _activityRepository.UpdateAsync(shiftedActivity, cancellationToken);

        var returnActivity = new MissionActivityModel
        {
            MissionId = missionId,
            Step = insertionStep,
            ActivityType = "RETURN_SUPPLIES",
            Description = $"Trả vật tư về kho {deliverActivity.DepotName} do giao thiếu so với kế hoạch (Activity #{deliverActivity.Id})",
            Priority = deliverActivity.Priority,
            EstimatedTime = deliverActivity.EstimatedTime,
            SosRequestId = deliverActivity.SosRequestId,
            DepotId = deliverActivity.DepotId,
            DepotName = deliverActivity.DepotName,
            DepotAddress = deliverActivity.DepotAddress,
            Items = JsonSerializer.Serialize(surplusItems),
            Status = MissionActivityStatus.Planned
        };

        var returnActivityId = await _activityRepository.AddAsync(returnActivity, cancellationToken);

        if (deliverActivity.MissionTeamId.HasValue)
            await _activityRepository.AssignTeamAsync(returnActivityId, deliverActivity.MissionTeamId.Value, cancellationToken);

        _logger.LogInformation(
            "Auto-created RETURN_SUPPLIES ActivityId={returnActivityId} for surplus from DELIVER_SUPPLIES ActivityId={deliverActivityId} MissionId={missionId}",
            returnActivityId, deliverActivity.Id, missionId);

        return returnActivityId;
    }

    private static void MergeSurplusItems(
        MissionActivityModel returnActivity,
        IEnumerable<SupplyToCollectDto> surplusItems,
        int deliverActivityId)
    {
        var currentItems = string.IsNullOrWhiteSpace(returnActivity.Items)
            ? []
            : JsonSerializer.Deserialize<List<SupplyToCollectDto>>(returnActivity.Items, _jsonOpts) ?? [];

        foreach (var surplusItem in surplusItems)
        {
            if (!surplusItem.ItemId.HasValue || surplusItem.Quantity <= 0)
                continue;

            var existingItem = currentItems.FirstOrDefault(item => item.ItemId == surplusItem.ItemId);
            if (existingItem is null)
            {
                currentItems.Add(CloneSurplusItem(surplusItem));
                continue;
            }

            existingItem.Quantity += surplusItem.Quantity;
        }

        returnActivity.Items = JsonSerializer.Serialize(currentItems);

        var note = $"Bổ sung vật tư giao thiếu từ activity #{deliverActivityId}.";
        if (string.IsNullOrWhiteSpace(returnActivity.Description))
        {
            returnActivity.Description = note;
        }
        else if (!returnActivity.Description.Contains(note, StringComparison.Ordinal))
        {
            returnActivity.Description = $"{returnActivity.Description}{Environment.NewLine}{note}";
        }
    }

    private static SupplyToCollectDto CloneSurplusItem(SupplyToCollectDto item) => new()
    {
        ItemId = item.ItemId,
        ItemName = item.ItemName,
        ImageUrl = item.ImageUrl,
        Quantity = item.Quantity,
        Unit = item.Unit
    };
}

