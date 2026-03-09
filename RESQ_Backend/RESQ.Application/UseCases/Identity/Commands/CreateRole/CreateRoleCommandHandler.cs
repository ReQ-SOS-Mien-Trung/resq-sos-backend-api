using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.CreateRole;

public class CreateRoleCommandHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<CreateRoleCommand, CreateRoleResponse>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<CreateRoleResponse> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"Role '{request.Name}' đã tồn tại");

        var model = new RoleModel { Name = request.Name };
        var id = await _roleRepository.CreateAsync(model, cancellationToken);

        if (id == 0)
            throw new CreateFailedException("role");

        return new CreateRoleResponse { Id = id, Name = request.Name };
    }
}
