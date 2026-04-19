using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetUserPermissions;

public class GetUserPermissionsQueryHandler(
    IUserRepository userRepository,
    IPermissionRepository permissionRepository,
    IRoleRepository roleRepository
) : IRequestHandler<GetUserPermissionsQuery, GetUserPermissionsResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;

    public async Task<GetUserPermissionsResponse> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.UserId}");

        var permissions = await _permissionRepository.GetUserPermissionsAsync(user.Id, cancellationToken);
        var rolePermissions = user.RoleId.HasValue
            ? await _roleRepository.GetPermissionsAsync(user.RoleId.Value, cancellationToken)
            : [];

        return new GetUserPermissionsResponse
        {
            UserId = user.Id,
            RoleId = user.RoleId,
            Permissions = permissions.Select(p => new UserPermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description
            }).ToList(),
            RolePermissions = rolePermissions.Select(p => new UserPermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description
            }).ToList()
        };
    }
}
