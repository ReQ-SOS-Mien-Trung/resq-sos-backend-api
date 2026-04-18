using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.DecommissionReusableItem;

public class DecommissionReusableItemCommandHandler(
    IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<DecommissionReusableItemCommand, DecommissionReusableItemResponse>
{
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IFirebaseService _firebaseService = firebaseService;

    public async Task<DecommissionReusableItemResponse> Handle(DecommissionReusableItemCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho ngưng hoạt động hoặc đã đóng. Không thể ngừng sử dụng thiết bị.");

        await _depotInventoryRepository.DecommissionReusableItemAsync(
            request.ReusableItemId,
            request.Note,
            request.UserId,
            cancellationToken);

        // Push notification cho manager xác nhận
        await _firebaseService.SendNotificationToUserAsync(
            request.UserId,
            "Ngừng sử dụng thiết bị",
            $"Đã ngừng sử dụng (decommission) thiết bị #{request.ReusableItemId}.",
            "inventory_disposal",
            new Dictionary<string, string>
            {
                ["reusableItemId"] = request.ReusableItemId.ToString(),
                ["reason"] = "Damaged"
            },
            cancellationToken);

        return new DecommissionReusableItemResponse(
            $"Đã ngừng sử dụng thiết bị #{request.ReusableItemId}.");
    }
}
