using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ExportInventory;

public class ExportInventoryCommandHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository)
    : IRequestHandler<ExportInventoryCommand, ExportInventoryResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;

    public async Task<ExportInventoryResponse> Handle(ExportInventoryCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho ngưng hoạt động hoặc đã đóng. Không thể xuất hàng khỏi kho này.");

        await _depotInventoryRepository.ExportInventoryAsync(
            depotId,
            request.ItemModelId,
            request.Quantity,
            request.UserId,
            request.Note,
            cancellationToken);

        return new ExportInventoryResponse($"Đã xuất kho thành công {request.Quantity} đơn vị vật tư #{request.ItemModelId} tại kho #{depotId}.");
    }
}
