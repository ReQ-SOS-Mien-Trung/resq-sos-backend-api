using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.AdjustInventory;

public class AdjustInventoryCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository)
    : IRequestHandler<AdjustInventoryCommand, AdjustInventoryResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<AdjustInventoryResponse> Handle(AdjustInventoryCommand request, CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("T�i kho?n hi?n t?i kh�ng du?c ch? d?nh qu?n l� b?t k? kho n�o dang ho?t d?ng.");

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho ngung ho?t d?ng ho?c d� d�ng. Kh�ng th? di?u ch?nh t?n kho.");

        await _depotInventoryRepository.AdjustInventoryAsync(
            depotId,
            request.ItemModelId,
            request.QuantityChange,
            request.UserId,
            request.Reason,
            request.Note,
            request.ExpiredDate,
            cancellationToken);

        var direction = request.QuantityChange > 0 ? "tang" : "gi?m";
        var absQty    = Math.Abs(request.QuantityChange);
        return new AdjustInventoryResponse(
            $"�� di?u ch?nh {direction} {absQty} don v? v?t ph?m #{request.ItemModelId} t?i kho #{depotId}.");
    }
}
