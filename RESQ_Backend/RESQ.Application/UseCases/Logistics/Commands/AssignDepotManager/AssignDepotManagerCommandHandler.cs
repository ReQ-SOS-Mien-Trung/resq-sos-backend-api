using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;

public class AssignDepotManagerCommandHandler(
    IDepotRepository depotRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<AssignDepotManagerCommandHandler> logger)
    : IRequestHandler<AssignDepotManagerCommand, AssignDepotManagerResponse>
{
    public async Task<AssignDepotManagerResponse> Handle(
        AssignDepotManagerCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "AssignDepotManager: depotId={DepotId}, managerId={ManagerId}",
            request.DepotId, request.ManagerId);

        // 1. Validate depot tồn tại
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với ID = {request.DepotId}");

        // 2. Validate user tồn tại và là Manager (RoleId = 4)
        var user = await userRepository.GetByIdAsync(request.ManagerId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy người dùng với ID = {request.ManagerId}");

        if (user.RoleId != 4)
            throw new BadRequestException(
                $"Người dùng {user.LastName} {user.FirstName} không có vai trò Quản lý kho (Manager).");

        // 3. Kiểm tra manager không đang quản lý kho khác
        var isBusy = await depotRepository.IsManagerActiveElsewhereAsync(
            request.ManagerId, request.DepotId, cancellationToken);
        if (isBusy)
            throw new BadRequestException(
                $"Người dùng {user.LastName} {user.FirstName} đang quản lý một kho khác. Vui lòng gỡ họ khỏi kho hiện tại trước khi gán vào kho này.");

        // 4. Gọi domain method - unassign manager cũ + assign mới + status → Available
        depot.AssignManager(request.ManagerId);

        // 5. Persist qua repository method chuyên biệt
        await depotRepository.AssignManagerAsync(depot, cancellationToken);
        await unitOfWork.SaveAsync();

        return new AssignDepotManagerResponse
        {
            DepotId    = depot.Id,
            DepotName  = depot.Name,
            Status     = depot.Status.ToString(),
            ManagerId  = request.ManagerId,
            ManagerFullName = $"{user.LastName} {user.FirstName}".Trim(),
            ManagerEmail    = user.Email,
            AssignedAt = depot.CurrentManager!.AssignedAt
        };
    }
}
