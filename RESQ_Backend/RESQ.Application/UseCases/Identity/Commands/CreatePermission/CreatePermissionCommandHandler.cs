using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.CreatePermission;

public class CreatePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<CreatePermissionCommand, CreatePermissionResponse>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<CreatePermissionResponse> Handle(CreatePermissionCommand request, CancellationToken cancellationToken)
    {
        var existing = await _permissionRepository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"Permission với code '{request.Code}' đã tồn tại");

        var model = new PermissionModel
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description
        };
        var id = await _permissionRepository.CreateAsync(model, cancellationToken);
        if (id == 0)
            throw new CreateFailedException("quyền hạn");

        return new CreatePermissionResponse
        {
            Id = id,
            Code = model.Code,
            Name = model.Name,
            Description = model.Description
        };
    }
}
