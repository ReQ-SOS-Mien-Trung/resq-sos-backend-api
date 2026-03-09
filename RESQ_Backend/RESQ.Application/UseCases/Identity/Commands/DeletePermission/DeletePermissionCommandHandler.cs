using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.DeletePermission;

public class DeletePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<DeletePermissionCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Unit> Handle(DeletePermissionCommand request, CancellationToken cancellationToken)
    {
        var permission = await _permissionRepository.GetByIdAsync(request.PermissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy permission với ID {request.PermissionId}");

        await _permissionRepository.DeleteAsync(permission.Id, cancellationToken);
        await _unitOfWork.SaveAsync();

        return Unit.Value;
    }
}
