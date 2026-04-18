using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
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

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

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

        var supplies = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts) ?? [];
        var validSupplies = supplies.Where(s => s.ItemId.HasValue).ToList();
        var supplyLookup = validSupplies.ToDictionary(s => s.ItemId!.Value);

        foreach (var deliveredItem in request.ActualDeliveredItems)
        {
            if (!supplyLookup.ContainsKey(deliveredItem.ItemId))
            {
                throw new BadRequestException(
                    $"ItemId {deliveredItem.ItemId} không tồn tại trong danh sách vật phẩm của activity này.");
            }
        }

        var deliveredLookup = request.ActualDeliveredItems.ToDictionary(d => d.ItemId);
        foreach (var supply in supplies)
        {
            if (supply.ItemId.HasValue && deliveredLookup.TryGetValue(supply.ItemId.Value, out var deliveredItem))
                supply.ActualDeliveredQuantity = deliveredItem.ActualQuantity;
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

        int? surplusReturnActivityId = null;
        var surplusCandidates = validSupplies
            .Where(s => s.ItemId.HasValue
                && deliveredLookup.TryGetValue(s.ItemId.Value, out var deliveredItem)
                && deliveredItem.ActualQuantity < s.Quantity)
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
                    throw new BadRequestException($"Không tìm thấy metadata vật phẩm #{itemId}.");

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
            surplusReturnActivityId = await UpsertSurplusReturnActivityAsync(activity, surplusItems, cancellationToken);
            await _unitOfWork.SaveAsync();
        }

        var normalizedDeliveryNote = request.DeliveryNote?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDeliveryNote))
            await SaveDeliveryNoteToDraftReportAsync(activity, normalizedDeliveryNote, cancellationToken);

        _logger.LogInformation(
            "Team confirmed DELIVER_SUPPLIES ActivityId={activityId} MissionId={missionId}: {itemCount} item type(s) delivered. SurplusReturnActivityId={surplusReturn}",
            request.ActivityId,
            request.MissionId,
            validSupplies.Count,
            surplusReturnActivityId?.ToString() ?? "none");

        var resultItems = validSupplies
            .Where(s => s.ItemId.HasValue)
            .Select(s =>
            {
                var actualQty = deliveredLookup.TryGetValue(s.ItemId!.Value, out var deliveredItem)
                    ? deliveredItem.ActualQuantity
                    : s.Quantity;

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
                ? $"Xác nhận giao hàng thành công. Đã cập nhật activity trả hàng #{surplusReturnActivityId} cho vật phẩm giao thiếu."
                : "Xác nhận giao hàng thành công.",
            SurplusReturnActivityId = surplusReturnActivityId,
            DeliveredItems = resultItems
        };
    }

    private async Task<int> UpsertSurplusReturnActivityAsync(
        MissionActivityModel deliverActivity,
        List<SupplyToCollectDto> surplusItems,
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
                deliverActivity.Id,
                existingReturnActivity.Id,
                missionId);

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
            Description = $"Trả vật phẩm về kho {deliverActivity.DepotName} do giao thiếu so với kế hoạch (Activity #{deliverActivity.Id})",
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
            returnActivityId,
            deliverActivity.Id,
            missionId);

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

        var note = $"Bổ sung vật phẩm giao thiếu từ activity #{deliverActivityId}.";
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
}
