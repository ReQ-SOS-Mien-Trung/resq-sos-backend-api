using System.Text.Json;
using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

public class ConfirmMissionSupplyPickupCommandHandler(
    IMissionActivityRepository activityRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConfirmMissionSupplyPickupCommand, ConfirmMissionSupplyPickupResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ConfirmMissionSupplyPickupResponse> Handle(
        ConfirmMissionSupplyPickupCommand request, CancellationToken cancellationToken)
    {
        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy activity với ID {request.ActivityId}.");

        if (string.IsNullOrWhiteSpace(activity.Items))
            throw new BadRequestException("Activity này không có danh sách hàng hóa.");

        var supplies = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts) ?? [];

        // Build validated buffer usage lookup
        var bufferUsageByItemId = new Dictionary<int, MissionPickupBufferUsageDto>();
        if (request.BufferUsages is { Count: > 0 })
        {
            var supplyLookup = supplies
                .Where(s => s.ItemId.HasValue)
                .ToDictionary(s => s.ItemId!.Value);

            foreach (var usage in request.BufferUsages)
            {
                if (usage.BufferQuantityUsed <= 0)
                    continue;

                if (string.IsNullOrWhiteSpace(usage.BufferUsedReason))
                    throw new BadRequestException(
                        $"Phải cung cấp lý do khi sử dụng buffer cho item ID {usage.ItemId}.");

                if (!supplyLookup.TryGetValue(usage.ItemId, out var supply))
                    throw new BadRequestException(
                        $"Item ID {usage.ItemId} không có trong danh sách hàng hóa của activity này.");

                var maxBuffer = supply.BufferQuantity ?? 0;
                if (usage.BufferQuantityUsed > maxBuffer)
                    throw new BadRequestException(
                        $"Số lượng buffer sử dụng ({usage.BufferQuantityUsed}) vượt quá số đã dự trù ({maxBuffer}) cho item ID {usage.ItemId}.");

                bufferUsageByItemId[usage.ItemId] = usage;
            }
        }

        if (bufferUsageByItemId.Count > 0)
        {
            await MissionSupplyExecutionSnapshotHelper.SyncBufferUsageAsync(
                activity,
                bufferUsageByItemId,
                _activityRepository,
                cancellationToken);
            await _unitOfWork.SaveAsync();
        }

        // Re-read updated supplies to return in response
        var updatedActivity = bufferUsageByItemId.Count > 0
            ? await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            : activity;
        var updatedSupplies = string.IsNullOrWhiteSpace(updatedActivity?.Items)
            ? supplies
            : JsonSerializer.Deserialize<List<SupplyToCollectDto>>(updatedActivity.Items, _jsonOpts) ?? supplies;

        var missionId = activity.MissionId ?? request.MissionId;
        var message = bufferUsageByItemId.Count > 0
            ? "Đã ghi nhận thông tin sử dụng buffer. Số lượng thực tế (required + buffer) sẽ được trừ khỏi kho khi activity chuyển sang Succeed."
            : "Không có buffer nào được sử dụng. Activity có thể chuyển sang Succeed để trừ số lượng required.";

        return new ConfirmMissionSupplyPickupResponse
        {
            ActivityId = request.ActivityId,
            MissionId = missionId,
            Message = message,
            UpdatedSupplies = updatedSupplies
        };
    }
}
