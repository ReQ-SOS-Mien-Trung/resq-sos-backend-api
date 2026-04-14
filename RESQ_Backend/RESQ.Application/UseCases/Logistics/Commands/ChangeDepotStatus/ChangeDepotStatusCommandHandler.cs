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
            if (request.Status == DepotStatus.Closing)
            {
                throw new ForbiddenException("Chỉ Admin mới có quyền chuyển kho sang trạng thái Closing (Đang đóng kho).");
            }

            var managedDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.RequestedBy, request.DepotId, cancellationToken);
            if (!managedDepotId.HasValue)
            {
                throw ExceptionCodes.WithCode(
                    new ForbiddenException("Tài khoản quản lý kho chưa được gán kho phụ trách."),
                    LogisticsErrorCodes.DepotManagerNotAssigned);
            }

            if (managedDepotId.Value != request.Id)
                throw new ForbiddenException("Bạn chỉ có thể thay đổi trạng thái kho mình đang quản lý.");
        }

        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        if (request.Status == DepotStatus.Unavailable || request.Status == DepotStatus.Closing)
        {
            var (asSource, asRequester) = await _depotRepository.GetNonTerminalSupplyRequestCountsAsync(request.Id, cancellationToken);
            if (asSource + asRequester > 0)
            {
                throw new ConflictException(
                    $"Kho hiện có {asSource + asRequester} đơn tiếp tế chưa hoàn tất " +
                    $"({asSource} là kho nguồn, {asRequester} là kho yêu cầu). " +
                    "Hãy hoàn thành hoặc huỷ tất cả đơn tiếp tế trước khi chuyển khỏi trạng thái này.");
            }

            var hasMissionCommitments = await _depotInventoryRepository.HasActiveInventoryCommitmentsAsync(request.Id, cancellationToken);
            if (hasMissionCommitments)
            {
                throw new ConflictException(
                    "Kho đang có vật phẩm được đặt trước hoặc đang sử dụng trong nhiệm vụ cứu hộ đang diễn ra. " +
                    "Hãy hoàn thành hoặc huỷ nhiệm vụ trước khi chuyển trạng thái kho này.");
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
            Message = "Cập nhật trạng thái kho thành công."
        };
    }
}
