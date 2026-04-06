using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;

public class AdjustInventoryCommandHandler(
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository)
    : IRequestHandler<AdjustInventoryCommand, AdjustInventoryResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;

    public async Task<AdjustInventoryResponse> Handle(AdjustInventoryCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho đang trong quá trình đóng hoặc đã đóng. Không thể điều chỉnh tồn kho.");

        await _depotInventoryRepository.AdjustInventoryAsync(
            depotId,
            request.ItemModelId,
            request.QuantityChange,
            request.UserId,
            request.Reason,
            request.Note,
            request.ExpiredDate,
            cancellationToken);

        var direction = request.QuantityChange > 0 ? "tăng" : "giảm";
        var absQty    = Math.Abs(request.QuantityChange);
        return new AdjustInventoryResponse(
            $"Đã điều chỉnh {direction} {absQty} đơn vị vật tư #{request.ItemModelId} tại kho #{depotId}.");
    }
}
