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
            "AssignDepotManager: depotId={DepotId}, managerIds=[{ManagerIds}]",
            request.DepotId, string.Join(", ", request.ManagerIds));

        // 1. Validate depot tồn tại
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với ID = {request.DepotId}");

        // 2. Batch-load tất cả user, sau đó validate trước khi thực hiện bất kỳ thay đổi nào
        var users = await userRepository.GetByIdsAsync(request.ManagerIds, cancellationToken);

        foreach (var managerId in request.ManagerIds)
        {
            var user = users.FirstOrDefault(u => u.Id == managerId)
                ?? throw new NotFoundException($"Không tìm thấy người dùng với ID = {managerId}");

            if (user.RoleId != 4)
                throw new BadRequestException(
                    $"Người dùng {user.LastName} {user.FirstName} không có vai trò Quản lý kho (Manager).");
        }

        // 3. Gán từng manager: domain method + persist (mỗi người một lần)
        foreach (var managerId in request.ManagerIds)
        {
            depot.AssignManager(managerId);
            await depotRepository.AssignManagerAsync(depot, managerId, request.RequestedBy, cancellationToken);
        }

        await unitOfWork.SaveAsync();

        // 4. Xây dựng response
        var assignedManagers = request.ManagerIds
            .Select(id =>
            {
                var user = users.First(u => u.Id == id);
                var assignment = depot.ManagerHistory
                    .Where(a => a.UserId == id && a.IsActive())
                    .OrderByDescending(a => a.AssignedAt)
                    .First();
                return new AssignedManagerInfo
                {
                    ManagerId  = id,
                    FullName   = $"{user.LastName} {user.FirstName}".Trim(),
                    Email      = user.Email,
                    AssignedAt = assignment.AssignedAt
                };
            })
            .ToList();

        return new AssignDepotManagerResponse
        {
            DepotId          = depot.Id,
            DepotName        = depot.Name,
            Status           = depot.Status.ToString(),
            AssignedManagers = assignedManagers
        };
    }
}
