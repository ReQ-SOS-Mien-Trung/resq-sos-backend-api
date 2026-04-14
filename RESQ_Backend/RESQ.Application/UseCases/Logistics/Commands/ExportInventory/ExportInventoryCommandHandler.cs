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
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken);

        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho ngung ho?t d?ng ho?c d� d�ng. Kh�ng th? xu?t h�ng kh?i kho n�y.");

        await _depotInventoryRepository.ExportInventoryAsync(
            depotId,
            request.ItemModelId,
            request.Quantity,
            request.UserId,
            request.Note,
            cancellationToken);

        return new ExportInventoryResponse($"�� xu?t kho th�nh c�ng {request.Quantity} don v? v?t ph?m #{request.ItemModelId} t?i kho #{depotId}.");
    }
}
