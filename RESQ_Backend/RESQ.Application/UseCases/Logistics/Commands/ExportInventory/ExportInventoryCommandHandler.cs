using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ExportInventory;

public class ExportInventoryCommandHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IManagerDepotAccessService managerDepotAccessService)
    : IRequestHandler<ExportInventoryCommand, ExportInventoryResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<ExportInventoryResponse> Handle(ExportInventoryCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho ngưng hoạt động hoặc đã đóng. Không thể xuất hàng khỏi kho này.");

        await _depotInventoryRepository.ExportInventoryAsync(
            depotId,
            request.ItemModelId,
            request.Quantity,
            request.UserId,
            request.Note,
            cancellationToken);

        return new ExportInventoryResponse($"Đã xuất kho thành công {request.Quantity} đơn vị vật phẩm #{request.ItemModelId} tại kho #{depotId}.");
    }
}
