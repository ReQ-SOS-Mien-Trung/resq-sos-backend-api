using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SetRolePermissions;

public class SetRolePermissionsCommandHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<SetRolePermissionsCommand, SetRolePermissionsResponse>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SetRolePermissionsResponse> Handle(SetRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy role với ID {request.RoleId}");

        await _roleRepository.SetPermissionsAsync(role.Id, request.PermissionIds, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new SetRolePermissionsResponse
        {
            RoleId = role.Id,
            PermissionIds = request.PermissionIds
        };
    }
}
