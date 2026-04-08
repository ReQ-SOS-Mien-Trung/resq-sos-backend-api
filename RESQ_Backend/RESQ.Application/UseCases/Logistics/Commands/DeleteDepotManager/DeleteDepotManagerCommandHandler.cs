using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.DeleteDepotManager;

public class DeleteDepotManagerCommandHandler(
    IDepotRepository depotRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeleteDepotManagerCommandHandler> logger)
    : IRequestHandler<DeleteDepotManagerCommand, DeleteDepotManagerResponse>
{
    public async Task<DeleteDepotManagerResponse> Handle(
        DeleteDepotManagerCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("DeleteDepotManager: depotId={DepotId}", request.DepotId);

        // 1. Validate depot tồn tại
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với ID = {request.DepotId}");

        // 2. Kiểm tra kho đang có manager active
        if (depot.CurrentManager == null)
            throw new BadRequestException("Kho này hiện không có quản lý nào được gán.");

        // 3. Gọi domain method — kiểm tra state (Closing/Closed bị chặn), xoá khỏi danh sách
        //    State hợp lệ: Available, Full, UnderMaintenance → sau khi xoá → PendingAssignment
        depot.DeleteManager();

        // 4. Persist: xoá hẳn bản ghi khỏi bảng depot_managers + cập nhật status kho
        await depotRepository.DeleteManagerAsync(depot, cancellationToken);
        await unitOfWork.SaveAsync();

        return new DeleteDepotManagerResponse
        {
            DepotId   = depot.Id,
            DepotName = depot.Name,
            Status    = depot.Status.ToString(),
            DeletedAt = DateTime.UtcNow
        };
    }
}
