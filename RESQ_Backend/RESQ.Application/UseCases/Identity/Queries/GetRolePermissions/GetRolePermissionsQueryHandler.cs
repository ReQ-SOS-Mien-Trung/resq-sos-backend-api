using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRolePermissions;

public class GetRolePermissionsQueryHandler(IRoleRepository roleRepository)
    : IRequestHandler<GetRolePermissionsQuery, GetRolePermissionsResponse>
{
    private readonly IRoleRepository _roleRepository = roleRepository;

    public async Task<GetRolePermissionsResponse> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy role với ID {request.RoleId}");

        var permissions = await _roleRepository.GetPermissionsAsync(request.RoleId, cancellationToken);

        return new GetRolePermissionsResponse
        {
            RoleId = role.Id,
            Permissions = permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description
            }).ToList()
        };
    }
}
