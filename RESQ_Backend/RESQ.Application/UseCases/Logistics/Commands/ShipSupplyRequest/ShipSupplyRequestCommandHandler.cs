using MediatR;
using RESQ.Application.Common.StateMachines;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ShipSupplyRequest;

/// <summary>
/// Kho ngu?n xu?t h�ng (TransferOut) v� chuy?n tr?ng th�i sang Shipping (dang v?n chuy?n).
/// Inventory source depot gi?m tuong ?ng.
/// </summary>
public class ShipSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    ISupplyRequestRepository supplyRequestRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ShipSupplyRequestCommand, ShipSupplyRequestResponse>
{
    public async Task<ShipSupplyRequestResponse> Handle(ShipSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        var sr = await supplyRequestRepository.GetByIdAsync(request.SupplyRequestId, cancellationToken)
            ?? throw new NotFoundException($"Kh�ng t�m th?y y�u c?u cung c?p #{request.SupplyRequestId}.");

        SupplyRequestStateMachine.EnsureCanShip(sr.SourceStatus);

        var managerDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("T�i kho?n kh�ng qu?n l� kho n�o dang ho?t d?ng.");

        if (managerDepotId != sr.SourceDepotId)
            throw new SupplyRequestAccessDeniedException("B?n kh�ng ph?i manager c?a kho ngu?n trong y�u c?u n�y.");

        var depotStatus = await depotRepository.GetStatusByIdAsync(managerDepotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closed)
            throw new ConflictException("Kho ngu?n ngung ho?t d?ng ho?c d� d�ng. Kh�ng th? xu?t h�ng cho y�u c?u ti?p t?.");

        // Wrap trong transaction d? d?m b?o TransferOut + UpdateStatus d?ng b?
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await supplyRequestRepository.TransferOutAsync(
                sr.SourceDepotId, sr.Items, sr.Id, request.UserId, cancellationToken);

            await supplyRequestRepository.UpdateStatusAsync(sr.Id, "Shipping", "InTransit", null, cancellationToken);
        });

        // Notify requesting manager
        await firebaseService.SendNotificationToUserAsync(
            sr.RequestedBy,
            "v?t ph?m dang du?c v?n chuy?n",
            $"Y�u c?u ti?p t? s? {sr.Id}: h�ng d� xu?t kho v� dang v?n chuy?n d?n kho c?a b?n.",
            "supply_shipped",
            cancellationToken);

        return new ShipSupplyRequestResponse { Message = $"�� xu?t h�ng cho y�u c?u s? {sr.Id}." };
    }
}
