using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

public class ChangeDepotStatusCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IUserPermissionResolver permissionResolver,
    IUnitOfWork unitOfWork,
    ILogger<ChangeDepotStatusCommandHandler> logger)
    : IRequestHandler<ChangeDepotStatusCommand, ChangeDepotStatusResponse>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IUserPermissionResolver _permissionResolver = permissionResolver;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly ILogger<ChangeDepotStatusCommandHandler> _logger = logger;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<ChangeDepotStatusResponse> Handle(ChangeDepotStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling ChangeDepotStatusCommand for Id={Id} to Status={Status} RequestedBy={RequestedBy}",
            request.Id,
            request.Status,
            request.RequestedBy);

        var userPermissions = await _permissionResolver.GetEffectivePermissionCodesAsync(request.RequestedBy, cancellationToken);
        var isAdmin = userPermissions.Contains(PermissionConstants.InventoryGlobalManage, StringComparer.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            if (request.Status == DepotStatus.Closing)
            {
                throw new ForbiddenException("Ch? Admin m?i c� quy?n chuy?n kho sang tr?ng th�i Closing (�ang d�ng kho).");
            }

            var managedDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.RequestedBy, request.DepotId, cancellationToken);
            if (!managedDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("T�i kho?n qu?n l� kho chua du?c g�n kho ph? tr�ch."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managedDepotId.Value != request.Id)
                throw new ForbiddenException("B?n ch? c� th? thay d?i tr?ng th�i kho m�nh dang qu?n l�.");
        }

        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Kh�ng t�m th?y kho c?u tr?.");

        if (request.Status == DepotStatus.Unavailable || request.Status == DepotStatus.Closing)
        {
            var (asSource, asRequester) = await _depotRepository.GetNonTerminalSupplyRequestCountsAsync(request.Id, cancellationToken);
            if (asSource + asRequester > 0)
            {
                throw new ConflictException(
                    $"Kho hi?n c� {asSource + asRequester} don ti?p t? chua ho�n t?t " +
                    $"({asSource} l� kho ngu?n, {asRequester} l� kho y�u c?u). " +
                    "H�y ho�n th�nh ho?c hu? t?t c? don ti?p t? tru?c khi chuy?n kh?i tr?ng th�i n�y.");
            }

            var hasMissionCommitments = await _depotInventoryRepository.HasActiveInventoryCommitmentsAsync(request.Id, cancellationToken);
            if (hasMissionCommitments)
            {
                throw new ConflictException(
                    "Kho dang c� v?t ph?m du?c d?t tru?c ho?c dang s? d?ng trong nhi?m v? c?u h? dang di?n ra. " +
                    "H�y ho�n th�nh ho?c hu? nhi?m v? tru?c khi chuy?n tr?ng th�i kho n�y.");
            }
        }

        depot.ChangeStatus(request.Status);

        await _depotRepository.UpdateAsync(depot, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Depot status updated successfully: Id={Id}", request.Id);

        return new ChangeDepotStatusResponse
        {
            Id = depot.Id,
            Status = depot.Status.ToString(),
            Message = "C?p nh?t tr?ng th�i kho th�nh c�ng."
        };
    }
}
