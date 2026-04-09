using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesCommandHandler(
    IMissionActivityRepository activityRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<ConfirmDeliverySuppliesCommandHandler> logger
) : IRequestHandler<ConfirmDeliverySuppliesCommand, ConfirmDeliverySuppliesResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
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

        // 5. Dispatch UpdateActivityStatusCommand(Succeed) — reuses all side-effects:
        //    team location update, SOS sync, auto-chain next activity.
        await _mediator.Send(
            new UpdateActivityStatusCommand(request.ActivityId, MissionActivityStatus.Succeed, request.ConfirmedBy),
            cancellationToken);

        // 6. Auto-create RETURN_SUPPLIES for any surplus (planned > actual delivered)
        int? surplusReturnActivityId = null;
        var surplusItems = validSupplies
            .Where(s => s.ItemId.HasValue
                && deliveredLookup.TryGetValue(s.ItemId.Value, out var d)
                && d.ActualQuantity < s.Quantity)
            .Select(s => new SupplyToCollectDto
            {
                ItemId = s.ItemId,
                ItemName = s.ItemName,
                Unit = s.Unit,
                Quantity = s.Quantity - deliveredLookup[s.ItemId!.Value].ActualQuantity
            })
            .ToList();

        if (surplusItems.Count > 0 && activity.DepotId.HasValue)
        {
            surplusReturnActivityId = await CreateSurplusReturnActivityAsync(
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
                ? $"Xác nhận giao hàng thành công. Đã tạo activity trả hàng #{surplusReturnActivityId} cho vật tư giao thiếu."
                : "Xác nhận giao hàng thành công.",
            SurplusReturnActivityId = surplusReturnActivityId,
            DeliveredItems = resultItems
        };
    }

    private async Task<int> CreateSurplusReturnActivityAsync(
        MissionActivityModel deliverActivity,
        List<SupplyToCollectDto> surplusItems,
        Guid decidedBy,
        CancellationToken cancellationToken)
    {
        var missionId = deliverActivity.MissionId ?? 0;
        var existingActivities = await _activityRepository.GetByMissionIdAsync(missionId, cancellationToken);
        var maxStep = existingActivities.Any() ? existingActivities.Max(a => a.Step ?? 0) : 0;

        var returnActivity = new MissionActivityModel
        {
            MissionId = missionId,
            Step = maxStep + 1,
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
}

