using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdatePermission;

public class UpdatePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<UpdatePermissionCommand, UpdatePermissionResponse>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<UpdatePermissionResponse> Handle(UpdatePermissionCommand request, CancellationToken cancellationToken)
    {
        var permission = await _permissionRepository.GetByIdAsync(request.PermissionId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy permission với ID {request.PermissionId}");

        var duplicate = await _permissionRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (duplicate is not null && duplicate.Id != request.PermissionId)
            throw new ConflictException($"Permission với code '{request.Code}' đã tồn tại");

        permission.Code = request.Code;
        permission.Name = request.Name;
        permission.Description = request.Description;

        await _permissionRepository.UpdateAsync(permission, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdatePermissionResponse
        {
            Id = permission.Id,
            Code = permission.Code,
            Name = permission.Name,
            Description = permission.Description
        };
    }
}
