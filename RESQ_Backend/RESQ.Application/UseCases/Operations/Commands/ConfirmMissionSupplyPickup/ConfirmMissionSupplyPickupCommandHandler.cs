using System.Text.Json;
using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmMissionSupplyPickup;

public class ConfirmMissionSupplyPickupCommandHandler(
    IMissionActivityRepository activityRepository,
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<ConfirmMissionSupplyPickupCommand, ConfirmMissionSupplyPickupResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ConfirmMissionSupplyPickupResponse> Handle(
        ConfirmMissionSupplyPickupCommand request, CancellationToken cancellationToken)
    {
        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy activity với ID {request.ActivityId}.");

        if (!activity.DepotId.HasValue)
            throw new BadRequestException("Activity này không có kho liên kết, không thể xác nhận lấy hàng.");

        if (string.IsNullOrWhiteSpace(activity.Items))
            throw new BadRequestException("Activity này không có danh sách hàng hóa.");

        var supplies = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts) ?? [];

        var items = supplies
            .Where(s => s.ItemId.HasValue && s.Quantity > 0)
            .Select(s => (ItemModelId: s.ItemId!.Value, s.Quantity))
            .ToList();

        if (items.Count == 0)
            throw new BadRequestException("Không có hàng hóa hợp lệ trong activity để xác nhận lấy.");

        var depotId = activity.DepotId.Value;
        var missionId = activity.MissionId ?? request.MissionId;

        await _depotInventoryRepository.ConsumeReservedSuppliesAsync(
            depotId, items, request.UserId, request.ActivityId, missionId, cancellationToken);

        return new ConfirmMissionSupplyPickupResponse
        {
            ActivityId = request.ActivityId,
            MissionId = missionId,
            DepotId = depotId,
            Message = "Xác nhận lấy hàng thành công. Số lượng trong kho đã được cập nhật.",
            ConsumedItems = supplies
                .Where(s => s.ItemId.HasValue && s.Quantity > 0)
                .Select(s => new ConsumedSupplyItemDto
                {
                    ItemModelId = s.ItemId!.Value,
                    ItemName = s.ItemName ?? string.Empty,
                    Quantity = s.Quantity
                })
                .ToList()
        };
    }
}
