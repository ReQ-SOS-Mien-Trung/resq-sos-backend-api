using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;

public class AdjustInventoryCommandHandler(
    IDepotInventoryRepository depotInventoryRepository)
    : IRequestHandler<AdjustInventoryCommand, AdjustInventoryResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;

    public async Task<AdjustInventoryResponse> Handle(AdjustInventoryCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

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
