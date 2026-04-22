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
        if (depotStatus is DepotStatus.Created or DepotStatus.PendingAssignment or DepotStatus.Closed or DepotStatus.Closing)
            throw new ConflictException("Kho hiện không ở trạng thái cho phép tiêu hủy vật phẩm. Kho phải đang hoạt động (Available) hoặc tạm ngừng (Unavailable).");

        await _depotInventoryRepository.DecommissionReusableItemAsync(
            depotId,
            request.ReusableItemId,
            request.Note,
            request.UserId,
            cancellationToken);

        await _firebaseService.SendNotificationToUserAsync(
            request.UserId,
            "Tiêu hủy vật phẩm tái sử dụng",
            $"Đã tiêu hủy vật phẩm tái sử dụng #{request.ReusableItemId}.",
            "inventory_disposal",
            new Dictionary<string, string>
            {
                ["reusableItemId"] = request.ReusableItemId.ToString(),
                ["reason"] = "Damaged"
            },
            cancellationToken);

        return new DecommissionReusableItemResponse(
            $"Đã tiêu hủy vật phẩm tái sử dụng #{request.ReusableItemId}.");
    }
}
