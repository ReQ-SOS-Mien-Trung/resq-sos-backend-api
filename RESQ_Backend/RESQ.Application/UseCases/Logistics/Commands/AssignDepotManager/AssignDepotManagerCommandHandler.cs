using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;

public class AssignDepotManagerCommandHandler(
    IDepotRepository depotRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<AssignDepotManagerCommandHandler> logger)
    : IRequestHandler<AssignDepotManagerCommand, AssignDepotManagerResponse>
{
    private const string DuplicateActiveDepotManagerConstraint = "uix_depot_managers_active_depot_user";

    public async Task<AssignDepotManagerResponse> Handle(
        AssignDepotManagerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "AssignDepotManager: depotId={DepotId}, managerIds=[{ManagerIds}]",
                request.DepotId, string.Join(", ", request.ManagerIds));

            var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy kho với ID = {request.DepotId}");

            var users = await userRepository.GetByIdsAsync(request.ManagerIds, cancellationToken);

            foreach (var managerId in request.ManagerIds)
            {
                var user = users.FirstOrDefault(u => u.Id == managerId)
                    ?? throw new NotFoundException($"Không tìm thấy người dùng với ID = {managerId}");

                if (user.RoleId != 4)
                {
                    throw new BadRequestException(
                        $"Người dùng {user.LastName} {user.FirstName} không có vai trò Quản lý kho (Manager).");
                }
            }

            foreach (var managerId in request.ManagerIds)
            {
                depot.AssignManager(managerId);
                await depotRepository.AssignManagerAsync(depot, managerId, request.RequestedBy, cancellationToken);
            }

            await unitOfWork.SaveAsync();

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
                        ManagerId = id,
                        FullName = $"{user.LastName} {user.FirstName}".Trim(),
                        Email = user.Email,
                        AssignedAt = assignment.AssignedAt
                    };
                })
                .ToList();

            return new AssignDepotManagerResponse
            {
                DepotId = depot.Id,
                DepotName = depot.Name,
                Status = depot.Status.ToString(),
                AssignedManagers = assignedManagers
            };
        }
        catch (DepotManagerAlreadyAssignedException ex)
        {
            throw new ConflictException(ex.Message);
        }
        catch (Exception ex) when (IsDuplicateActiveDepotManagerAssignment(ex))
        {
            throw new ConflictException("Quản lý này đã được gán active cho kho này rồi.");
        }
    }

    private static bool IsDuplicateActiveDepotManagerAssignment(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString();
            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current)?.ToString();

            if (string.Equals(sqlState, "23505", StringComparison.Ordinal)
                && string.Equals(constraintName, DuplicateActiveDepotManagerConstraint, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.InnerException!;
        }

        return false;
    }
}
