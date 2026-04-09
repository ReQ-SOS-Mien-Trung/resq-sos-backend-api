using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.Exceptions;
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
        _logger.LogInformation("Handling ChangeDepotStatusCommand for Id={Id} to Status={Status} RequestedBy={By}", request.Id, request.Status, request.RequestedBy);

        // ── Ownership check: nếu không phải admin thì chỉ được thạo tác kho của mình ──
        var userPermissions = await _permissionResolver.GetEffectivePermissionCodesAsync(request.RequestedBy, cancellationToken);
        var isAdmin = userPermissions.Contains(PermissionConstants.InventoryGlobalManage, StringComparer.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            var managedDepotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.RequestedBy, cancellationToken);
            if (managedDepotId != request.Id)
                throw new ForbiddenException("Bạn chỉ có thể thay đổi trạng thái kho mình đang quản lý.");
        }

        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken);
        if (depot == null)
        {
            throw new NotFoundException("Không tìm thấy kho cứu trợ");
        }

        // ── Guard: transition to Unavailable requires no active commitments ──
        if (request.Status == DepotStatus.Unavailable)
        {
            // 1. Check for active supply requests (both as source and as requester)
            var (asSource, asRequester) = await _depotRepository.GetNonTerminalSupplyRequestCountsAsync(request.Id, cancellationToken);
            if (asSource + asRequester > 0)
                throw new ConflictException(
                    $"Kho hiện có {asSource + asRequester} đơn tiếp tế chưa hoàn tất " +
                    $"({asSource} là kho nguồn, {asRequester} là kho yêu cầu). " +
                    "Hoàn thành hoặc hủy tất cả đơn tiếp tế trước khi chuyển sang Unavailable.");

            // 2. Check for active mission supply activities (reserved consumables / reusable InUse)
            var hasMissionCommitments = await _depotInventoryRepository.HasActiveInventoryCommitmentsAsync(request.Id, cancellationToken);
            if (hasMissionCommitments)
                throw new ConflictException(
                    "Kho đang có vật tư được đặt trữ hoặc đang sử dụng trong nhiệm vụ cứu hộ đang diễn ra. " +
                    "Chờ hoàn thành hoặc hủy nhiệm vụ trước khi chuyển sang Unavailable.");
        }

        // Domain Logic validation is handled inside the Entity's ChangeStatus method
        depot.ChangeStatus(request.Status);

        await _depotRepository.UpdateAsync(depot, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Depot status updated successfully: Id={Id}", request.Id);

        return new ChangeDepotStatusResponse
        {
            Id = depot.Id,
            Status = depot.Status.ToString(),
            Message = "Cập nhật trạng thái kho thành công."
        };
    }
}
