using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.AssignRoleToUser;

public class AssignRoleToUserCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork,
    ILogger<AssignRoleToUserCommandHandler> logger
) : IRequestHandler<AssignRoleToUserCommand, AssignRoleToUserResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AssignRoleToUserCommandHandler> _logger = logger;

    public async Task<AssignRoleToUserResponse> Handle(AssignRoleToUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.UserId}");

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy role với ID {request.RoleId}");

        user.RoleId = role.Id;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Assigned role {RoleId} to user {UserId}", role.Id, user.Id);

        return new AssignRoleToUserResponse { UserId = user.Id, RoleId = role.Id };
    }
}
