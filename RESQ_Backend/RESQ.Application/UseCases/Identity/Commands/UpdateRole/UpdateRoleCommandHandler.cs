using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRole;

public class UpdateRoleCommandHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<UpdateRoleCommand, UpdateRoleResponse>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<UpdateRoleResponse> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy role với ID {request.RoleId}");

        var duplicate = await _roleRepository.GetByNameAsync(request.Name, cancellationToken);
        if (duplicate is not null && duplicate.Id != request.RoleId)
            throw new ConflictException($"Role '{request.Name}' đã tồn tại");

        role.Name = request.Name;
        await _roleRepository.UpdateAsync(role, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new UpdateRoleResponse { Id = role.Id, Name = role.Name };
    }
}
