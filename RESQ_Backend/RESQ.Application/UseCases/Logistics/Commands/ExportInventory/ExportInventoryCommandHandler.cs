using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ExportInventory;

public class ExportInventoryCommandHandler(
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<ExportInventoryCommand, ExportInventoryResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<ExportInventoryResponse> Handle(ExportInventoryCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

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
