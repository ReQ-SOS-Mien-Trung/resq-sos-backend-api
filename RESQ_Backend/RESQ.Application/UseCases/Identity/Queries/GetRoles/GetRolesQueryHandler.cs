using MediatR;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRoles;

public class GetRolesQueryHandler(IRoleRepository roleRepository)
    : IRequestHandler<GetRolesQuery, List<GetRolesItemResponse>>
{
    private readonly IRoleRepository _roleRepository = roleRepository;

    public async Task<List<GetRolesItemResponse>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.GetAllAsync(cancellationToken);
        return roles.Select(r => new GetRolesItemResponse { Id = r.Id, Name = r.Name }).ToList();
    }
}
