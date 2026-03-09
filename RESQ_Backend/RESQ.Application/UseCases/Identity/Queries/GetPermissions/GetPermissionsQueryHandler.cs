using MediatR;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetPermissions;

public class GetPermissionsQueryHandler(IPermissionRepository permissionRepository)
    : IRequestHandler<GetPermissionsQuery, List<GetPermissionsItemResponse>>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;

    public async Task<List<GetPermissionsItemResponse>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await _permissionRepository.GetAllAsync(cancellationToken);
        return permissions.Select(p => new GetPermissionsItemResponse
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description
        }).ToList();
    }
}
