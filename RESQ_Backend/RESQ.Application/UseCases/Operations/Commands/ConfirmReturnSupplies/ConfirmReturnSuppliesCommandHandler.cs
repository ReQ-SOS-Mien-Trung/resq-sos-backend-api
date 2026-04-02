using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

public class ConfirmReturnSuppliesCommandHandler(
    IMissionActivityRepository activityRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<ConfirmReturnSuppliesCommandHandler> logger
) : IRequestHandler<ConfirmReturnSuppliesCommand, ConfirmReturnSuppliesResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ConfirmReturnSuppliesCommandHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ConfirmReturnSuppliesResponse> Handle(
        ConfirmReturnSuppliesCommand request, CancellationToken cancellationToken)
    {
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

        var supplies = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts) ?? [];
        var validItems = supplies
            .Where(s => s.ItemId.HasValue && s.Quantity > 0)
            .ToList();

        if (validItems.Count == 0)
            throw new BadRequestException("Không có hàng hóa hợp lệ trong activity để xác nhận trả.");

        var depotId = activity.DepotId.Value;
        var missionId = activity.MissionId ?? request.MissionId;

        // Transition: PendingConfirmation → Succeed
        await _activityRepository.UpdateStatusAsync(request.ActivityId, MissionActivityStatus.Succeed, request.ConfirmedBy, cancellationToken);

        // Restock inventory
        foreach (var item in validItems)
        {
            try
            {
                await _depotInventoryRepository.AdjustInventoryAsync(
                    depotId,
                    item.ItemId!.Value,
                    item.Quantity,
                    request.ConfirmedBy,
                    "RETURN_SUPPLIES",
                    $"Vật tư được trả về từ activity #{request.ActivityId}, mission #{missionId}",
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to restock ItemId={itemId} Qty={qty} for ActivityId={activityId} DepotId={depotId}",
                    item.ItemId, item.Quantity, request.ActivityId, depotId);
            }
        }

        await _unitOfWork.SaveAsync();

        _logger.LogInformation(
            "Depot manager confirmed RETURN_SUPPLIES ActivityId={activityId} DepotId={depotId}: {count} item type(s) restocked",
            request.ActivityId, depotId, validItems.Count);

        return new ConfirmReturnSuppliesResponse
        {
            ActivityId = request.ActivityId,
            MissionId = missionId,
            DepotId = depotId,
            Message = "Xác nhận trả hàng thành công. Vật tư đã được nhập lại kho.",
            RestoredItems = validItems.Select(s => new RestoredSupplyItemDto
            {
                ItemModelId = s.ItemId!.Value,
                ItemName = s.ItemName ?? string.Empty,
                Quantity = s.Quantity
            }).ToList()
        };
    }
}
