using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SetUserPermissions;

public class SetUserPermissionsCommandHandler(
    IUserRepository userRepository,
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<SetUserPermissionsCommand, SetUserPermissionsResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SetUserPermissionsResponse> Handle(SetUserPermissionsCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.TargetUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.TargetUserId}");

        await _permissionRepository.SetUserPermissionsAsync(user.Id, request.AdminId, request.PermissionIds, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new SetUserPermissionsResponse
        {
            UserId = user.Id,
            PermissionIds = request.PermissionIds
        };
    }
}
