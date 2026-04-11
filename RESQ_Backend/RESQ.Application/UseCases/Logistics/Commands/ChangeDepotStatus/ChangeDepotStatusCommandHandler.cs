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
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IUserPermissionResolver permissionResolver,
    IUnitOfWork unitOfWork,
    ILogger<ChangeDepotStatusCommandHandler> logger)
    : IRequestHandler<ChangeDepotStatusCommand, ChangeDepotStatusResponse>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IUserPermissionResolver _permissionResolver = permissionResolver;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ChangeDepotStatusCommandHandler> _logger = logger;

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
            var managedDepotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.RequestedBy, cancellationToken);
            if (!managedDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managedDepotId.Value != request.Id)
                throw new ForbiddenException("Ban chi co the thay doi trang thai kho minh dang quan ly.");
        }

        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Kh�ng t�m th?y kho c?u tr?");

        if (request.Status == DepotStatus.Unavailable)
        {
            var (asSource, asRequester) = await _depotRepository.GetNonTerminalSupplyRequestCountsAsync(request.Id, cancellationToken);
            if (asSource + asRequester > 0)
            {
                throw new ConflictException(
                    $"Kho hien co {asSource + asRequester} don tiep te chua hoan tat " +
                    $"({asSource} la kho nguon, {asRequester} la kho yeu cau). " +
                    "Hoan thanh hoac huy tat ca don tiep te truoc khi chuyen sang Unavailable.");
            }

            var hasMissionCommitments = await _depotInventoryRepository.HasActiveInventoryCommitmentsAsync(request.Id, cancellationToken);
            if (hasMissionCommitments)
            {
                throw new ConflictException(
                    "Kho dang co vat tu duoc dat tru hoac dang su dung trong nhiem vu cuu ho dang dien ra. " +
                    "Cho hoan thanh hoac huy nhiem vu truoc khi chuyen sang Unavailable.");
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
